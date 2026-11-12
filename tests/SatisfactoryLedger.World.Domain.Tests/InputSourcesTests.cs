namespace SatisfactoryLedger.World.Domain.Tests;

/// <summary>
/// Where what arrives over a factory's input comes from (decided 2026-11-11): followed upstream through logistics to
/// the machines, providers and containers at the starts of its line; where lines merged, from each in proportion to what
/// it brings.
/// </summary>
public class InputSourcesTests
{
    private static BuildableId Id(string name) => BuildableId.Of(name);

    /// <summary>What each buildable sends on, per minute, as the analysis found it; nothing for the others.</summary>
    private static Func<BuildableId, double> Sending(params (string Id, double PerMinute)[] flows) =>
        id => flows.Where(flow => flow.Id == id.Id).Sum(flow => flow.PerMinute);

    private static Dictionary<string, double> Starts(IReadOnlyDictionary<BuildableId, double> starts) =>
        starts.ToDictionary(start => start.Key.Id, start => Math.Round(start.Value, 4));

    [Fact]
    public void What_arrives_on_a_plain_belt_all_comes_from_the_machine_at_its_start()
    {
        WorldGraph world = new TestWorld()
            .Machine("smelter", Items.IronOre, 30, Items.IronIngot, 30).Belt("ingots")
            .Connect("smelter", "ingots").Build();

        var starts = InputSources.Of(world, Id("ingots"), Items.IronIngot, 20, Sending(("ingots", 20)));

        Assert.Equal(new Dictionary<string, double> { ["smelter"] = 20 }, Starts(starts));
    }

    [Fact]
    public void Where_lines_merge_it_comes_from_each_in_proportion_to_what_each_brings()
    {
        // 30 from smelter A and 10 from smelter B merge; 20 of the 40 arrive.
        WorldGraph world = new TestWorld()
            .Machine("smelter A", Items.IronOre, 30, Items.IronIngot, 30).Belt("from A")
            .Machine("smelter B", Items.IronOre, 30, Items.IronIngot, 30).Belt("from B")
            .Merger("merge").Belt("ingots")
            .Connect("smelter A", "from A", "merge", "ingots").Connect("smelter B", "from B", "merge").Build();

        var starts = InputSources.Of(world, Id("ingots"), Items.IronIngot, 20, Sending(("from A", 30), ("from B", 10), ("ingots", 40)));

        Assert.Equal(new Dictionary<string, double> { ["smelter A"] = 15, ["smelter B"] = 5 }, Starts(starts));
    }

    [Fact]
    public void A_container_nothing_feeds_is_where_it_comes_from()
    {
        WorldGraph world = new TestWorld()
            .Container("box", (Items.IronIngot, 500)).Belt("ingots")
            .Connect("box", "ingots").Build();

        var starts = InputSources.Of(world, Id("ingots"), Items.IronIngot, 30, Sending());

        Assert.Equal(new Dictionary<string, double> { ["box"] = 30 }, Starts(starts));
    }

    [Fact]
    public void A_line_that_loops_back_is_followed_once()
    {
        // A belt from the merger goes round and back into it; the ingots came from the smelter.
        WorldGraph world = new TestWorld()
            .Machine("smelter", Items.IronOre, 30, Items.IronIngot, 30).Belt("ingots")
            .Merger("merge").Belt("on").Splitter("split").Belt("round").Belt("out")
            .Connect("smelter", "ingots", "merge", "on", "split", "out")
            .Connect("split", "round", "merge").Build();

        var starts = InputSources.Of(world, Id("out"), Items.IronIngot, 30, Sending(("ingots", 30), ("round", 10), ("on", 40), ("out", 30)));

        Assert.Equal(new Dictionary<string, double> { ["smelter"] = 30 }, Starts(starts));
    }

    /// <summary>What each buildable has to spare, or each piece of logistics has room for, per minute.</summary>
    private static Func<BuildableId, double> PerMinute(params (string Id, double PerMinute)[] values) =>
        id => values.Where(value => value.Id == id.Id).Sum(value => value.PerMinute);

    [Fact]
    public void What_its_sources_have_to_spare_can_come_when_the_line_has_room()
    {
        WorldGraph world = new TestWorld().Miner("miner").Belt("ore").Connect("miner", "ore").Build();

        double more = InputSources.CouldBringMore(world, [Id("ore")], PerMinute(("miner", 60)), PerMinute(("ore", 200)), PerMinute(), PerMinute());

        Assert.Equal(60, more, 4);
    }

    [Fact]
    public void A_full_belt_on_the_way_lets_nothing_more_through()
    {
        WorldGraph world = new TestWorld().Miner("miner").Belt("full").Belt("ore").Connect("miner", "full", "ore").Build();

        double more = InputSources.CouldBringMore(world, [Id("ore")], PerMinute(("miner", 60)), PerMinute(("full", 0), ("ore", 200)), PerMinute(), PerMinute());

        Assert.Equal(0, more, 4);
    }

    [Fact]
    public void The_tightest_room_on_the_way_decides()
    {
        // The plant needs 500 a minute on a belt that carries 480: 20 more, though the miner has 100 to spare.
        WorldGraph world = new TestWorld().Miner("miner").Belt("to plant").Connect("miner", "to plant").Build();

        double more = InputSources.CouldBringMore(world, [Id("to plant")], PerMinute(("miner", 100)), PerMinute(("to plant", 20)), PerMinute(), PerMinute());

        Assert.Equal(20, more, 4);
    }

    [Fact]
    public void Sources_sharing_a_narrow_belt_share_its_room()
    {
        // Two miners with 50 each to spare merge onto a belt with room for 30 more.
        WorldGraph world = new TestWorld()
            .Miner("miner A").Belt("a").Miner("miner B").Belt("b").Merger("merge").Belt("ore")
            .Connect("miner A", "a", "merge", "ore").Connect("miner B", "b", "merge").Build();

        double more = InputSources.CouldBringMore(
            world, [Id("ore")], PerMinute(("miner A", 50), ("miner B", 50)), PerMinute(("a", 100), ("b", 100), ("merge", double.PositiveInfinity), ("ore", 30)), PerMinute(), PerMinute());

        Assert.Equal(30, more, 4);
    }

    [Fact]
    public void Each_of_its_inputs_takes_what_its_own_line_can_bring()
    {
        // A splitter sends a miner's ore to two of its inputs, each with room for 20; the miner has 100 to spare.
        WorldGraph world = new TestWorld()
            .Miner("miner").Belt("ore").Splitter("split").Belt("left").Belt("right")
            .Connect("miner", "ore", "split", "left").Connect("split", "right").Build();

        double more = InputSources.CouldBringMore(
            world, [Id("left"), Id("right")], PerMinute(("miner", 100)), PerMinute(("ore", 100), ("split", double.PositiveInfinity), ("left", 20), ("right", 20)), PerMinute(), PerMinute());

        Assert.Equal(40, more, 4);
    }

    /// <summary>
    /// miner → line → split → (to us → [input], overflow → sink): the miner runs flat out and 30 a minute overflow into
    /// the sink.
    /// </summary>
    private static WorldGraph Overflowing() => new TestWorld()
        .Miner("miner").Belt("line").Splitter("split").Belt("to us").Belt("overflow").Sink("sink")
        .Connect("miner", "line", "split", "to us").Connect("split", "overflow", "sink").Build();

    [Fact]
    public void What_overflows_into_a_sink_could_come_instead()
    {
        double more = InputSources.CouldBringMore(
            Overflowing(), [Id("to us")], PerMinute(), PerMinute(("line", 0), ("split", double.PositiveInfinity), ("to us", 100), ("overflow", 100)),
            PerMinute(("line", 90), ("to us", 60), ("overflow", 30)), PerMinute(("sink", 30)));

        Assert.Equal(30, more, 4);
    }

    [Fact]
    public void What_overflows_into_a_sink_could_come_only_as_far_as_its_line_has_room()
    {
        double more = InputSources.CouldBringMore(
            Overflowing(), [Id("to us")], PerMinute(), PerMinute(("line", 0), ("split", double.PositiveInfinity), ("to us", 10), ("overflow", 100)),
            PerMinute(("line", 90), ("to us", 60), ("overflow", 30)), PerMinute(("sink", 30)));

        Assert.Equal(10, more, 4);
    }

    [Fact]
    public void What_another_machine_takes_is_not_there_to_take()
    {
        // As above, but the overflow feeds another factory's machine, not a sink.
        WorldGraph world = new TestWorld()
            .Miner("miner").Belt("line").Splitter("split").Belt("to us").Belt("overflow").Machine("smelter", Items.IronOre, 30, Items.IronIngot, 30)
            .Connect("miner", "line", "split", "to us").Connect("split", "overflow", "smelter").Build();

        double more = InputSources.CouldBringMore(
            world, [Id("to us")], PerMinute(), PerMinute(("line", 0), ("split", double.PositiveInfinity), ("to us", 100), ("overflow", 100)),
            PerMinute(("line", 90), ("to us", 60), ("overflow", 30)), PerMinute());

        Assert.Equal(0, more, 4);
    }
}
