using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.SaveFile.Tests;

/// <summary>From the save's connectors (two ends that point at each other) to connections with a direction.</summary>
public class ConnectionBuilderTests
{
    private static BuildableId B(string name) => BuildableId.Of(name);

    /// <summary>A connector "owner.port", linked to another connector "owner.port" (or to nothing).</summary>
    private static Connector C(string path, Port port, string? linkedTo = null) =>
        new(B(path.Split('.')[0]), path, port, linkedTo);

    private static IReadOnlyList<Connection> Build(params Connector[] connectors) =>
        ConnectionBuilder.Build(connectors, connectors.Select(connector => connector.Owner).ToHashSet());

    [Fact]
    public void Items_flow_from_an_output_to_the_input_it_is_linked_to()
    {
        var connections = Build(
            C("miner.Output0", Port.Out, "belt.ConveyorAny0"),
            C("belt.ConveyorAny0", Port.In, "miner.Output0"));

        Assert.Equal([new Connection(B("miner"), B("belt"))], connections);
    }

    [Fact]
    public void A_link_stored_on_one_end_only_is_rejected()
    {
        Assert.Throws<SaveFileException>(() => Build(
            C("miner.Output0", Port.Out, "belt.ConveyorAny0"),
            C("belt.ConveyorAny0", Port.In)));
    }

    [Fact]
    public void A_link_to_a_connector_that_is_not_in_the_save_is_rejected()
    {
        Assert.Throws<SaveFileException>(() => Build(C("miner.Output0", Port.Out, "nowhere.Input0")));
    }

    [Fact]
    public void Two_outputs_linked_to_each_other_are_rejected()
    {
        Assert.Throws<SaveFileException>(() => Build(
            C("left.Output0", Port.Out, "right.Output0"),
            C("right.Output0", Port.Out, "left.Output0")));
    }

    [Fact]
    public void A_connector_without_a_link_gives_no_connection()
    {
        Assert.Empty(Build(C("constructor.Input0", Port.In)));
    }

    [Fact]
    public void Fluid_flows_away_from_the_extractor_through_pipes_that_have_no_direction_of_their_own()
    {
        var connections = Build(
            C("extractor.Out", Port.Out, "pipe1.PipelineConnection0"),
            C("pipe1.PipelineConnection0", Port.Undirected, "extractor.Out"),
            C("pipe1.PipelineConnection1", Port.Undirected, "pipe2.PipelineConnection1"),
            C("pipe2.PipelineConnection1", Port.Undirected, "pipe1.PipelineConnection1"),
            C("pipe2.PipelineConnection0", Port.Undirected, "packager.PipeInputFactory"),
            C("packager.PipeInputFactory", Port.In, "pipe2.PipelineConnection0"));

        Assert.Equal(
            [new Connection(B("extractor"), B("pipe1")), new Connection(B("pipe1"), B("pipe2")), new Connection(B("pipe2"), B("packager"))],
            connections.OrderBy(connection => connection.From.Id, StringComparer.Ordinal));
    }

    [Fact]
    public void A_pipe_that_no_source_reaches_gets_no_direction()
    {
        var connections = Build(
            C("pipe1.PipelineConnection1", Port.Undirected, "pipe2.PipelineConnection0"),
            C("pipe2.PipelineConnection0", Port.Undirected, "pipe1.PipelineConnection1"));

        Assert.Empty(connections);
    }

    [Fact]
    public void A_link_to_a_buildable_that_is_not_in_the_world_is_dropped()
    {
        Connector[] connectors =
        [
            C("station.Output0", Port.Out, "belt.ConveyorAny0"),
            C("belt.ConveyorAny0", Port.In, "station.Output0"),
        ];

        var connections = ConnectionBuilder.Build(connectors, new HashSet<BuildableId> { B("belt") });

        Assert.Empty(connections);
    }
}
