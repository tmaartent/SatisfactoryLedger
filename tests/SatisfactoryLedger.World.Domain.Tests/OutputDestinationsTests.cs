using SatisfactoryLedger.World.Domain.Tests.Tracing;
using SatisfactoryLedger.World.Domain.Tracing;

namespace SatisfactoryLedger.World.Domain.Tests;

/// <summary>
/// Where a factory's output goes (BR-A18, decided 2026-11-11): followed through logistics to the machines, consumers
/// and containers at the ends of its line, shared at each split as the flow there is.
/// </summary>
public class OutputDestinationsTests
{
    private static BuildableId Id(string name) => BuildableId.Of(name);

    private static Dictionary<string, double> Ends(IReadOnlyDictionary<BuildableId, double> ends) =>
        ends.ToDictionary(end => end.Key.Id, end => Math.Round(end.Value, 4));

    [Fact]
    public void An_output_on_a_plain_belt_all_goes_to_the_machine_at_its_end()
    {
        WorldGraph world = new TestWorld()
            .Belt("plates").Machine("assembler", Items.IronPlate, 30, Items.ReinforcedIronPlate, 5)
            .Connect("plates", "assembler").Build();

        var ends = OutputDestinations.Of(world, Id("plates"), Items.IronPlate, 20, new TestReadings().Into("plates", Items.IronPlate, 20).Build(world));

        Assert.Equal(new Dictionary<string, double> { ["assembler"] = 20 }, Ends(ends));
    }

    [Fact]
    public void A_splitter_shares_it_as_the_flow_on_each_side_is()
    {
        WorldGraph world = new TestWorld()
            .Splitter("splitter").Belt("left").Belt("right")
            .Machine("assembler A", Items.IronPlate, 30, Items.ReinforcedIronPlate, 5)
            .Machine("assembler B", Items.IronPlate, 30, Items.ReinforcedIronPlate, 5)
            .Connect("splitter", "left", "assembler A").Connect("splitter", "right", "assembler B").Build();

        var ends = OutputDestinations.Of(
            world, Id("splitter"), Items.IronPlate, 30, new TestReadings().Into("left", Items.IronPlate, 20).Into("right", Items.IronPlate, 10).Build(world));

        Assert.Equal(new Dictionary<string, double> { ["assembler A"] = 20, ["assembler B"] = 10 }, Ends(ends));
    }

    [Fact]
    public void Where_other_supply_merges_in_it_goes_on_in_proportion_to_its_share_of_the_mix()
    {
        // 10 of ours and 30 from elsewhere merge; the 40 split evenly, so each side gets a quarter of ours on each.
        WorldGraph world = new TestWorld()
            .Belt("ours").Belt("theirs").Merger("merger").Belt("mixed").Splitter("splitter").Belt("left").Belt("right")
            .Machine("assembler A", Items.IronPlate, 30, Items.ReinforcedIronPlate, 5)
            .Machine("assembler B", Items.IronPlate, 30, Items.ReinforcedIronPlate, 5)
            .Connect("ours", "merger", "mixed", "splitter", "left", "assembler A")
            .Connect("theirs", "merger").Connect("splitter", "right", "assembler B").Build();

        FlowReadings readings = new TestReadings()
            .Into("ours", Items.IronPlate, 10).Into("theirs", Items.IronPlate, 30).Into("mixed", Items.IronPlate, 40)
            .Into("left", Items.IronPlate, 20).Into("right", Items.IronPlate, 20).Build(world);

        var ends = OutputDestinations.Of(world, Id("ours"), Items.IronPlate, 10, readings);

        Assert.Equal(new Dictionary<string, double> { ["assembler A"] = 5, ["assembler B"] = 5 }, Ends(ends));
    }

    [Fact]
    public void A_line_into_a_container_ends_there()
    {
        WorldGraph world = new TestWorld().Belt("plates").Container("box").Connect("plates", "box").Build();

        var ends = OutputDestinations.Of(
            world, Id("plates"), Items.IronPlate, 20, new TestReadings().Into("plates", Items.IronPlate, 20).Into("box", Items.IronPlate, 20).Build(world));

        Assert.Equal(new Dictionary<string, double> { ["box"] = 20 }, Ends(ends));
    }

    [Fact]
    public void A_belt_that_goes_nowhere_is_its_own_end()
    {
        WorldGraph world = new TestWorld().Belt("plates").Connect("plates").Build();

        var ends = OutputDestinations.Of(world, Id("plates"), Items.IronPlate, 20, new TestReadings().Into("plates", Items.IronPlate, 20).Build(world));

        Assert.Equal(new Dictionary<string, double> { ["plates"] = 20 }, Ends(ends));
    }

    [Fact]
    public void A_line_that_loops_back_is_followed_once()
    {
        // The splitter sends half back round to the merger before it: that half isn't followed a second time.
        WorldGraph world = new TestWorld()
            .Belt("plates").Merger("merger").Belt("on").Splitter("splitter").Belt("back").Belt("out").Sink("sink")
            .Connect("plates", "merger", "on", "splitter", "out", "sink").Connect("splitter", "back", "merger").Build();

        var ends = OutputDestinations.Of(world, Id("plates"), Items.IronPlate, 20, new TestReadings()
            .Into("plates", Items.IronPlate, 20).Into("on", Items.IronPlate, 20).Into("out", Items.IronPlate, 20).Build(world));

        Assert.Equal(new Dictionary<string, double> { ["sink"] = 20 }, Ends(ends));
    }
}
