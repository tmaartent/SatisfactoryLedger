using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.SaveFile.Tests;

/// <summary>
/// From the save's connectors (two ends that point at each other) to connections with a direction. Pipes have no
/// direction: each connected pipe network becomes one hub (BR-A6).
/// </summary>
public class ConnectionBuilderTests
{
    private static BuildableId B(string name) => BuildableId.Of(name);

    /// <summary>A connector "owner.port", linked to another connector "owner.port" (or to nothing).</summary>
    private static Connector C(string path, Port port, string? linkedTo = null) =>
        new(B(path.Split('.')[0]), path, port, linkedTo);

    private static IReadOnlyList<Connection> Build(params Connector[] connectors) => Wire(connectors).Connections;

    private static Wiring Wire(Connector[] connectors, params string[] fluidBuffers) =>
        ConnectionBuilder.Build(connectors, connectors.Select(connector => connector.Owner).ToHashSet(), fluidBuffers.Select(B).ToHashSet());

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

    /// <summary>Two connectors that point at each other: one link, stored on both ends as the save does.</summary>
    private static Connector[] Link(string one, Port onePort, string other, Port otherPort) =>
        [C(one, onePort, other), C(other, otherPort, one)];

    private static Connector[] Pipe(string one, string other) => Link(one, Port.Undirected, other, Port.Undirected);

    private static Connector[] Feeds(string output, string pipe) => Link(output, Port.Out, pipe, Port.Undirected);

    private static Connector[] FedBy(string pipe, string input) => Link(pipe, Port.Undirected, input, Port.In);

    private static IReadOnlyList<Connection> Sorted(IEnumerable<Connection> connections) =>
        [.. connections.OrderBy(connection => connection.From.Id, StringComparer.Ordinal).ThenBy(connection => connection.To.Id, StringComparer.Ordinal)];

    private static readonly BuildableId Hub = PipeNetwork.HubOf(B("a"));

    [Fact]
    public void A_pipe_network_takes_from_every_output_that_feeds_it_and_feeds_every_input_it_reaches()
    {
        // Two encoders feed a manifold from both ends; a collider takes from its middle.
        Wiring wiring = Wire(
        [
            .. Feeds("encoder1.PipeOutputFactory", "a.PipelineConnection0"),
            .. Feeds("encoder2.PipeOutputFactory", "c.PipelineConnection1"),
            .. Pipe("a.PipelineConnection1", "b.Connection0"),
            .. Pipe("b.Connection1", "c.PipelineConnection0"),
            .. Pipe("b.Connection2", "d.PipelineConnection0"),
            .. FedBy("d.PipelineConnection1", "collider.PipeInputFactory"),
        ]);

        Assert.Equal(
            Sorted(
            [
                new Connection(B("a"), Hub), new Connection(B("c"), Hub), new Connection(B("d"), B("collider")),
                new Connection(B("encoder1"), B("a")), new Connection(B("encoder2"), B("c")), new Connection(Hub, B("d")),
                        ]),
            Sorted(wiring.Connections));
    }

    [Fact]
    public void A_pipe_network_knows_its_pipes()
    {
        Wiring wiring = Wire(
        [
            .. Feeds("extractor.Out", "a.PipelineConnection0"),
            .. Pipe("a.PipelineConnection1", "b.PipelineConnection0"),
            .. FedBy("b.PipelineConnection1", "packager.PipeInputFactory"),
        ]);

        PipeNetwork network = Assert.Single(wiring.PipeNetworks);
        Assert.Equal(Hub, network.Hub);
        Assert.Equal([B("a"), B("b")], network.Pipes);
    }

    [Fact]
    public void A_pipe_from_an_output_straight_to_an_input_is_no_network()
    {
        Wiring wiring = Wire(
        [
            .. Feeds("extractor.Out", "a.PipelineConnection0"),
            .. FedBy("a.PipelineConnection1", "packager.PipeInputFactory"),
        ]);

        Assert.Equal(Sorted([new Connection(B("a"), B("packager")), new Connection(B("extractor"), B("a"))]), Sorted(wiring.Connections));
        Assert.Empty(wiring.PipeNetworks);
    }

    [Fact]
    public void A_fluid_buffer_in_a_network_something_feeds_passes_nothing()
    {
        // In steady state it's full.
        Wiring wiring = Wire(
        [
            .. Feeds("extractor.Out", "a.PipelineConnection0"),
            .. Pipe("a.PipelineConnection1", "tank.PipeAnyConnection0"),
            .. Pipe("tank.PipeAnyConnection1", "b.PipelineConnection0"),
            .. FedBy("b.PipelineConnection1", "packager.PipeInputFactory"),
        ], "tank");

        Assert.DoesNotContain(wiring.Connections, connection => connection.From == B("tank") || connection.To == B("tank"));
    }

    [Fact]
    public void A_fluid_buffer_feeds_a_network_nothing_else_feeds()
    {
        Wiring wiring = Wire(
        [
            .. Pipe("tank.PipeAnyConnection0", "a.PipelineConnection0"),
            .. FedBy("a.PipelineConnection1", "blender.PipeInputFactory"),
        ], "tank");

        BuildableId hub = PipeNetwork.HubOf(B("a"));
        Assert.Equal(
            Sorted([new Connection(B("a"), B("blender")), new Connection(hub, B("a")), new Connection(B("tank"), hub)]),
            Sorted(wiring.Connections));
    }

    [Fact]
    public void A_pipe_fed_by_an_output_that_also_feeds_an_input_sends_that_input_its_share_from_the_network()
    {
        Wiring wiring = Wire(
        [
            .. Feeds("extractor.Out", "a.PipelineConnection0"),
            .. FedBy("a.PipelineConnection2", "packager.PipeInputFactory"),
            .. Pipe("a.PipelineConnection1", "b.PipelineConnection0"),
            .. FedBy("b.PipelineConnection1", "refinery.PipeInputFactory"),
        ]);

        Assert.Equal(
            Sorted(
            [
                new Connection(B("a"), Hub), new Connection(B("b"), B("refinery")), new Connection(B("extractor"), B("a")),
                new Connection(Hub, B("b")), new Connection(Hub, B("packager")),
                        ]),
            Sorted(wiring.Connections));
    }

    [Fact]
    public void Pipes_that_nothing_feeds_and_that_feed_nothing_give_no_connections()
    {
        Assert.Empty(Build([.. Pipe("a.PipelineConnection1", "b.PipelineConnection0")]));
    }

    [Fact]
    public void A_link_to_a_buildable_that_is_not_in_the_world_is_dropped()
    {
        Connector[] connectors =
        [
            C("station.Output0", Port.Out, "belt.ConveyorAny0"),
            C("belt.ConveyorAny0", Port.In, "station.Output0"),
        ];

        var connections = ConnectionBuilder.Build(connectors, new HashSet<BuildableId> { B("belt") }, new HashSet<BuildableId>()).Connections;

        Assert.Empty(connections);
    }
}
