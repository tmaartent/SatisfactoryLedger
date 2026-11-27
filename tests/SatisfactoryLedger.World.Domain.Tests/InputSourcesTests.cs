using SatisfactoryLedger.World.Domain.Tests.Tracing;

namespace SatisfactoryLedger.World.Domain.Tests;

/// <summary>
/// Where what arrives over a factory's input comes from (decided 2026-11-11): followed upstream through logistics to
/// the machines, providers and containers at the starts of its line; where lines merged, from each in proportion to what
/// it brings.
/// </summary>
public class InputSourcesTests
{
    private static BuildableId Id(string name) => BuildableId.Of(name);

    private static Dictionary<string, double> Starts(IReadOnlyDictionary<BuildableId, double> starts) =>
        starts.ToDictionary(start => start.Key.Id, start => Math.Round(start.Value, 4));

    [Fact]
    public void What_arrives_on_a_plain_belt_all_comes_from_the_machine_at_its_start()
    {
        WorldGraph world = new TestWorld()
            .Machine("smelter", Items.IronOre, 30, Items.IronIngot, 30).Belt("ingots")
            .Connect("smelter", "ingots").Build();

        var starts = InputSources.Of(world, Id("ingots"), Items.IronIngot, 20, new TestReadings().Into("ingots", Items.IronIngot, 20).Build(world));

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

        var starts = InputSources.Of(world, Id("ingots"), Items.IronIngot, 20, new TestReadings()
            .Into("from A", Items.IronIngot, 30).Into("from B", Items.IronIngot, 10).Into("ingots", Items.IronIngot, 40).Build(world));

        Assert.Equal(new Dictionary<string, double> { ["smelter A"] = 15, ["smelter B"] = 5 }, Starts(starts));
    }

    [Fact]
    public void A_container_nothing_feeds_is_where_it_comes_from()
    {
        WorldGraph world = new TestWorld()
            .Container("box", (Items.IronIngot, 500)).Belt("ingots")
            .Connect("box", "ingots").Build();

        var starts = InputSources.Of(world, Id("ingots"), Items.IronIngot, 30, new TestReadings().Build(world));

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

        var starts = InputSources.Of(world, Id("out"), Items.IronIngot, 30, new TestReadings()
            .Into("ingots", Items.IronIngot, 30).Into("round", Items.IronIngot, 10).Into("on", Items.IronIngot, 40).Into("out", Items.IronIngot, 30)
            .Build(world));

        Assert.Equal(new Dictionary<string, double> { ["smelter"] = 30 }, Starts(starts));
    }
}
