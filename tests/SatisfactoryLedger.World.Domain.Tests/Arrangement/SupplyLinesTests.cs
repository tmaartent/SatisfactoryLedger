using SatisfactoryLedger.World.Domain.Arrangement;

namespace SatisfactoryLedger.World.Domain.Tests.Arrangement;

/// <summary>
/// Supply lines (decided 2026-11-18, BR-S): what brings an item runs as a line past the factories that need it, each
/// tapping what it takes, topped up by a priority merger when the line runs short; instead of a belt from each source to
/// each factory.
/// </summary>
public class SupplyLinesTests
{
    /// <summary>Ends one square each, along a row: the number is how far east.</summary>
    private static Dictionary<string, GridRectangle> Row(params (string Id, int X)[] ends) =>
        ends.ToDictionary(end => end.Id, end => new GridRectangle(end.X, 0, 1, 1));

    private static IReadOnlyList<SupplyLeg> Plan(
        double capacity, bool pipe, SupplyEnd[] sources, SupplyEnd[] consumers, Dictionary<string, GridRectangle> ends) =>
        SupplyLines.Plan(new ItemSupply("Desc_OreIron_C", pipe, capacity, sources, consumers), ends);

    [Fact]
    public void A_line_runs_past_the_factories_and_a_priority_merger_tops_it_up_before_the_one_it_cannot_feed()
    {
        // The owner's example: miners of 1200; factories taking 500, 400 and 600 in a row; more miners by the third.
        IReadOnlyList<SupplyLeg> legs = Plan(
            1200,
            pipe: false,
            [new SupplyEnd("miners", 1200), new SupplyEnd("more", 600)],
            [new SupplyEnd("first", 500), new SupplyEnd("second", 400), new SupplyEnd("third", 600)],
            Row(("miners", 0), ("first", 10), ("second", 20), ("third", 30), ("more", 32)));

        Assert.Equal(
            [
                new SupplyLeg("miners", "first", "Desc_OreIron_C", Pipe: false, 1200, Injection: false),
                new SupplyLeg("first", "second", "Desc_OreIron_C", Pipe: false, 700, Injection: false),
                new SupplyLeg("second", "third", "Desc_OreIron_C", Pipe: false, 300, Injection: false),
                new SupplyLeg("more", "third", "Desc_OreIron_C", Pipe: false, 300, Injection: true, Onto: 2),
            ],
            legs);
    }

    [Fact]
    public void A_line_taps_the_nearest_factory_next()
    {
        IReadOnlyList<SupplyLeg> legs = Plan(
            1200,
            pipe: false,
            [new SupplyEnd("miners", 900)],
            [new SupplyEnd("far", 300), new SupplyEnd("near", 300), new SupplyEnd("middle", 300)],
            Row(("miners", 0), ("far", 30), ("near", 10), ("middle", 20)));

        Assert.Equal([("miners", "near"), ("near", "middle"), ("middle", "far")], legs.Select(leg => (leg.From, leg.To)));
    }

    [Fact]
    public void An_injection_comes_from_the_source_nearest_the_factory_that_would_come_short()
    {
        IReadOnlyList<SupplyLeg> legs = Plan(
            1200,
            pipe: false,
            [new SupplyEnd("miners", 600), new SupplyEnd("far", 500), new SupplyEnd("near", 500)],
            [new SupplyEnd("smelters", 1000)],
            Row(("miners", 0), ("far", 50), ("smelters", 10), ("near", 12)));

        Assert.Equal(("near", 400), legs.Where(leg => leg.Injection).Select(leg => (leg.From, leg.PerMinute)).Single());
    }

    [Fact]
    public void An_injection_fills_the_line_for_the_factories_after_too_up_to_the_belt()
    {
        // 200 left after the first; the second needs 500 and the third 300: the merger brings 600, not 300.
        IReadOnlyList<SupplyLeg> legs = Plan(
            1200,
            pipe: false,
            [new SupplyEnd("miners", 700), new SupplyEnd("more", 650)],
            [new SupplyEnd("first", 500), new SupplyEnd("second", 500), new SupplyEnd("third", 300)],
            Row(("miners", 0), ("first", 10), ("more", 18), ("second", 20), ("third", 30)));

        Assert.Equal(600, legs.Single(leg => leg.Injection).PerMinute);
        Assert.Equal(300, legs.Single(leg => leg.To == "third").PerMinute);
    }

    [Fact]
    public void The_overflow_of_a_top_up_runs_on_past_a_smart_splitter_to_the_next_merger()
    {
        // The belt is full after the merger at the second: 700 of the top-up's 1000 overflow, and run on to top up the
        // third (decided 2026-11-18). The top-up belt to the second carries what both mergers take.
        IReadOnlyList<SupplyLeg> legs = Plan(
            1200,
            pipe: false,
            [new SupplyEnd("miners", 1200), new SupplyEnd("more", 1000)],
            [new SupplyEnd("first", 300), new SupplyEnd("second", 1000), new SupplyEnd("third", 500)],
            Row(("miners", 0), ("first", 10), ("more", 15), ("second", 20), ("third", 30)));

        Assert.Equal(
            [
                new SupplyLeg("miners", "first", "Desc_OreIron_C", Pipe: false, 1200, Injection: false),
                new SupplyLeg("first", "second", "Desc_OreIron_C", Pipe: false, 900, Injection: false),
                new SupplyLeg("more", "second", "Desc_OreIron_C", Pipe: false, 600, Injection: true, Onto: 1),
                new SupplyLeg("second", "third", "Desc_OreIron_C", Pipe: false, 200, Injection: false),
                new SupplyLeg("second", "third", "Desc_OreIron_C", Pipe: false, 300, Injection: true, Overflow: true, Onto: 3, Off: 2),
            ],
            legs);
    }

    [Fact]
    public void The_overflow_of_a_top_up_may_start_a_line_of_its_own_and_feed_the_factory_it_stands_at()
    {
        // The first needs more than a belt: its line ends full there. The top-up's overflow feeds it the rest straight
        // from the smart splitter, no belt on the floor, and runs on as a line to the second.
        IReadOnlyList<SupplyLeg> legs = Plan(
            1200,
            pipe: false,
            [new SupplyEnd("miners", 1100), new SupplyEnd("more", 1000)],
            [new SupplyEnd("first", 1500), new SupplyEnd("second", 300)],
            Row(("miners", 0), ("first", 10), ("more", 12), ("second", 40)));

        // The top-up brings 100 to fill the belt, then 300 straight into the first and 300 on to the second.
        Assert.Equal(
            [
                new SupplyLeg("miners", "first", "Desc_OreIron_C", Pipe: false, 1100, Injection: false),
                new SupplyLeg("more", "first", "Desc_OreIron_C", Pipe: false, 700, Injection: true, Onto: 0),
                new SupplyLeg("first", "second", "Desc_OreIron_C", Pipe: false, 300, Injection: false, Overflow: true, Off: 1),
            ],
            legs);
    }

    [Fact]
    public void A_line_carries_no_more_than_its_factories_take()
    {
        IReadOnlyList<SupplyLeg> legs = Plan(
            1200,
            pipe: false,
            [new SupplyEnd("miners", 1200)],
            [new SupplyEnd("smelters", 450)],
            Row(("miners", 0), ("smelters", 10)));

        Assert.Equal(450, legs.Single().PerMinute);
    }

    [Fact]
    public void A_factory_that_needs_more_than_a_belt_is_fed_by_another_line_too()
    {
        IReadOnlyList<SupplyLeg> legs = Plan(
            1200,
            pipe: false,
            [new SupplyEnd("miners", 1200), new SupplyEnd("more", 300)],
            [new SupplyEnd("smelters", 1500)],
            Row(("miners", 0), ("smelters", 10), ("more", 20)));

        Assert.Equal([("miners", 1200.0, false), ("more", 300.0, false)], legs.Select(leg => (leg.From, leg.PerMinute, leg.Injection)));
    }

    [Fact]
    public void A_pipe_has_no_injections_the_factory_takes_what_is_left_and_a_new_pipe_starts()
    {
        // Water: 300 from the first extractors, 200 and 300 needed.
        IReadOnlyList<SupplyLeg> legs = Plan(
            600,
            pipe: true,
            [new SupplyEnd("extractors", 300), new SupplyEnd("more", 250)],
            [new SupplyEnd("first", 200), new SupplyEnd("second", 300)],
            Row(("extractors", 0), ("first", 10), ("second", 20), ("more", 22)));

        Assert.Equal(
            [
                new SupplyLeg("extractors", "first", "Desc_OreIron_C", Pipe: true, 300, Injection: false),
                new SupplyLeg("first", "second", "Desc_OreIron_C", Pipe: true, 100, Injection: false),
                new SupplyLeg("more", "second", "Desc_OreIron_C", Pipe: true, 200, Injection: false),
            ],
            legs);
    }

    [Fact]
    public void A_factory_never_feeds_itself()
    {
        // The factory makes the item and needs it from elsewhere too.
        IReadOnlyList<SupplyLeg> legs = Plan(
            1200,
            pipe: false,
            [new SupplyEnd("refinery", 600), new SupplyEnd("miners", 300)],
            [new SupplyEnd("refinery", 300), new SupplyEnd("smelters", 600)],
            Row(("refinery", 0), ("smelters", 10), ("miners", 20)));

        Assert.DoesNotContain(legs, leg => leg.From == leg.To);
        Assert.Equal(300, legs.Where(leg => leg.To == "refinery").Sum(leg => leg.PerMinute));
        Assert.Equal(600, legs.Where(leg => leg.To == "smelters").Sum(leg => leg.PerMinute));
    }

    [Fact]
    public void Ends_off_the_floor_are_left_out()
    {
        IReadOnlyList<SupplyLeg> legs = Plan(
            1200,
            pipe: false,
            [new SupplyEnd("miners", 600)],
            [new SupplyEnd("outside", 300), new SupplyEnd("smelters", 300)],
            Row(("miners", 0), ("smelters", 10)));

        Assert.Equal([("miners", "smelters", 300.0)], legs.Select(leg => (leg.From, leg.To, leg.PerMinute)));
    }
}
