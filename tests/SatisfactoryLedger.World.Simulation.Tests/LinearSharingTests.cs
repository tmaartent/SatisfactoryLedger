using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Tests;

using static SatisfactoryLedger.World.Domain.Tests.TestFlow;

namespace SatisfactoryLedger.World.Simulation.Tests;

/// <summary>
/// Splitters and mergers share evenly, each on its own (BR-A4, BR-A5): the sequence of linear programmes, part C
/// (ADR-0026). The cases are the sweep's.
/// </summary>
public class LinearSharingTests
{
    private const double Precision = 0.0001;

    private static readonly ItemId Ore = Items.IronOre;
    private static readonly ItemId Ingot = Items.IronIngot;

    private static LinearFlow Solve(TestFlow flow) => LinearSteadyState.Solve(flow.Network());

    // ---- BR-A4: splitters -------------------------------------------------------------------------------------

    [Fact]
    public void A_splitter_divides_evenly_when_every_output_can_take_its_share()
    {
        LinearFlow result = Solve(new TestFlow()
            .Provider("miner", Ore, 60).Splitter("split")
            .Machine("left", Ore, 40, Ingot, 40).Machine("right", Ore, 40, Ingot, 40).Sink("sink")
            .Connect("miner", "split", "left", "sink")
            .Connect("split", "right", "sink"));

        Assert.Equal(0.75, result.Utilisation[Id("left")], Precision);
        Assert.Equal(0.75, result.Utilisation[Id("right")], Precision);
    }

    [Fact]
    public void What_one_splitter_output_cannot_take_goes_to_the_others()
    {
        LinearFlow result = Solve(new TestFlow()
            .Provider("miner", Ore, 60).Splitter("split")
            .Machine("small", Ore, 20, Ingot, 20).Machine("big", Ore, 40, Ingot, 40).Sink("sink")
            .Connect("miner", "split", "small", "sink")
            .Connect("split", "big", "sink"));

        Assert.Equal(20, result.Flow(Id("split"), Id("small"), Ore), Precision);
        Assert.Equal(40, result.Flow(Id("split"), Id("big"), Ore), Precision);
    }

    [Fact]
    public void A_manifold_short_of_supply_feeds_the_first_machines_more_than_the_last()
    {
        // 60 ore for three machines that want 30 each: 30, 15, 15, not 20 each.
        LinearFlow result = Solve(new TestFlow()
            .Provider("miner", Ore, 60).Splitter("s1").Splitter("s2")
            .Machine("m1", Ore, 30, Ingot, 30).Machine("m2", Ore, 30, Ingot, 30).Machine("m3", Ore, 30, Ingot, 30).Sink("sink")
            .Connect("miner", "s1", "m1", "sink")
            .Connect("s1", "s2", "m2", "sink")
            .Connect("s2", "m3", "sink"));

        Assert.Equal(1.0, result.Utilisation[Id("m1")], Precision);
        Assert.Equal(0.5, result.Utilisation[Id("m2")], Precision);
        Assert.Equal(0.5, result.Utilisation[Id("m3")], Precision);
    }

    [Fact]
    public void Each_splitter_divides_evenly_on_its_own_whatever_lies_beyond_it()
    {
        // The counter-example to one global fairness (ADR-0026): 10 → S1 → (sink a, S2 → sinks c, d).
        LinearFlow result = Solve(new TestFlow()
            .Provider("miner", Ore, 10).Splitter("s1").Splitter("s2").Sink("a").Sink("c").Sink("d")
            .Connect("miner", "s1", "a")
            .Connect("s1", "s2", "c")
            .Connect("s2", "d"));

        Assert.Equal(5, result.Flow(Id("s1"), Id("a"), Ore), Precision);
        Assert.Equal(2.5, result.Flow(Id("s2"), Id("c"), Ore), Precision);
        Assert.Equal(2.5, result.Flow(Id("s2"), Id("d"), Ore), Precision);
    }

    [Fact]
    public void A_splitter_short_of_supply_feeds_a_sink_and_a_machine_alike()
    {
        // The game splits evenly; the machine isn't served first because it makes something.
        LinearFlow result = Solve(new TestFlow()
            .Provider("miner", Ore, 20).Splitter("split")
            .Machine("smelter", Ore, 30, Ingot, 30).Sink("overflow").Sink("sink")
            .Connect("miner", "split", "smelter", "sink")
            .Connect("split", "overflow"));

        Assert.Equal(10, result.Flow(Id("split"), Id("overflow"), Ore), Precision);
        Assert.Equal(10, result.Flow(Id("split"), Id("smelter"), Ore), Precision);
    }

    [Fact]
    public void A_splitter_output_into_a_belt_that_ends_takes_nothing_and_the_others_get_it_all()
    {
        // A belt with nothing at its end fills up and stops: the splitter sends everything the other way.
        LinearFlow result = Solve(new TestFlow()
            .Provider("miner", Ore, 60).Splitter("split").Belt("dead end").Sink("sink")
            .Connect("miner", "split", "dead end")
            .Connect("split", "sink"));

        Assert.Equal(60, result.Flow(Id("split"), Id("sink"), Ore), Precision);
        Assert.Equal(0, result.Flow(Id("split"), Id("dead end"), Ore), Precision);
    }

    // ---- BR-A5: mergers ---------------------------------------------------------------------------------------

    [Fact]
    public void A_merger_whose_output_takes_everything_takes_all_its_inputs_bring()
    {
        LinearFlow result = Solve(new TestFlow()
            .Provider("a", Ore, 10).Provider("b", Ore, 20).Merger("merge").Belt("out").Sink("sink")
            .Connect("a", "merge", "out", "sink")
            .Connect("b", "merge"));

        Assert.Equal(10, result.Flow(Id("a"), Id("merge"), Ore), Precision);
        Assert.Equal(20, result.Flow(Id("b"), Id("merge"), Ore), Precision);
    }

    [Fact]
    public void A_merger_over_capacity_takes_evenly_from_its_inputs()
    {
        LinearFlow result = Solve(new TestFlow()
            .Provider("a", Ore, 40).Provider("b", Ore, 40).Merger("merge").Belt("mk1", capacity: 60).Sink("sink")
            .Connect("a", "merge", "mk1", "sink")
            .Connect("b", "merge"));

        Assert.Equal(30, result.Flow(Id("a"), Id("merge"), Ore), Precision);
        Assert.Equal(30, result.Flow(Id("b"), Id("merge"), Ore), Precision);
    }

    [Fact]
    public void A_merger_input_that_offers_less_leaves_its_share_to_the_others()
    {
        LinearFlow result = Solve(new TestFlow()
            .Provider("a", Ore, 10).Provider("b", Ore, 60).Merger("merge").Belt("mk1", capacity: 60).Sink("sink")
            .Connect("a", "merge", "mk1", "sink")
            .Connect("b", "merge"));

        Assert.Equal(10, result.Flow(Id("a"), Id("merge"), Ore), Precision);
        Assert.Equal(50, result.Flow(Id("b"), Id("merge"), Ore), Precision);
    }

    [Fact]
    public void Two_belts_bringing_the_same_item_to_a_machine_share_what_it_asks()
    {
        LinearFlow result = Solve(new TestFlow()
            .Provider("miner 1", Ore, 30).Belt("ore 1").Provider("miner 2", Ore, 30).Belt("ore 2")
            .Machine("smelter", Ore, 30, Ingot, 30).Belt("ingots").Sink("sink")
            .Connect("miner 1", "ore 1", "smelter", "ingots", "sink")
            .Connect("miner 2", "ore 2", "smelter"));

        Assert.Equal(15, result.Flow(Id("ore 1"), Id("smelter"), Ore), Precision);
        Assert.Equal(15, result.Flow(Id("ore 2"), Id("smelter"), Ore), Precision);
    }
}
