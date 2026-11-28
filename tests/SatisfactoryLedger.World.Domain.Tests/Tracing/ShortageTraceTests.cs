using SatisfactoryLedger.World.Domain.Tracing;

namespace SatisfactoryLedger.World.Domain.Tests.Tracing;

/// <summary>
/// Where a shortage starts (decided 2026-11-20): from starved machines, upstream along the item they're short of, through
/// producers that are starved themselves, to the first place that holds the item back for a reason of its own.
/// </summary>
public class ShortageTraceTests
{
    private static BuildableId[] Ids(params string[] ids) => [.. ids.Select(BuildableId.Of)];

    private static TraceStart Single(IReadOnlyList<TraceStart> starts) => Assert.Single(starts);

    [Fact]
    public void Producers_running_at_full_speed_that_send_it_all_here_are_too_few()
    {
        // The smelter makes 30 ingots at full speed; the constructor needs 60.
        WorldGraph world = new TestWorld()
            .Machine("smelter", Items.IronOre, 30, Items.IronIngot, 30).Belt("ingots").Machine("plates", Items.IronIngot, 60, Items.IronPlate, 40)
            .Connect("smelter", "ingots", "plates").Build();
        FlowReadings readings = new TestReadings().Running("smelter").Starved("plates", Items.IronIngot, 0.5).Into("ingots", Items.IronIngot, 30).Build(world);

        TraceStart start = Single(ShortageTrace.From(world, readings, Ids("plates")));

        Assert.Equal(TraceCause.TooFewProducers, start.Cause);
        Assert.Equal(Items.IronIngot, start.Item);
        Assert.Equal(Ids("smelter"), start.At);
        Assert.Equal(30, start.PerMinute);
        TraceStep step = Assert.Single(start.Path);
        Assert.Equal(Ids("plates"), step.Machines);
        Assert.Equal((Items.IronIngot, 30.0), (step.Item, step.PerMinute));
    }

    [Fact]
    public void A_full_belt_on_the_way_is_where_it_starts_and_the_producers_behind_it_are_not_looked_at()
    {
        WorldGraph world = new TestWorld()
            .Machine("smelter", Items.IronOre, 60, Items.IronIngot, 60).Belt("ingots").Belt("mk1", 60)
            .Machine("plates", Items.IronIngot, 120, Items.IronPlate, 80)
            .Connect("smelter", "ingots", "mk1", "plates").Build();
        FlowReadings readings = new TestReadings()
            .Starved("smelter", Items.IronOre, 0.5).Bottleneck("mk1", 30).Starved("plates", Items.IronIngot, 0.5).Into("mk1", Items.IronIngot, 60).Build(world);

        TraceStart start = Single(ShortageTrace.From(world, readings, Ids("plates")));

        Assert.Equal(TraceCause.BeltFull, start.Cause);
        Assert.Equal(Ids("mk1"), start.At);
    }

    [Fact]
    public void A_full_belt_says_what_it_takes_to_carry_what_is_offered_to_it()
    {
        WorldGraph world = new TestWorld()
            .Machine("smelter", Items.IronOre, 60, Items.IronIngot, 60).Belt("ingots").Belt("mk1", 60)
            .Machine("plates", Items.IronIngot, 120, Items.IronPlate, 80)
            .Connect("smelter", "ingots", "mk1", "plates").Build();
        FlowReadings readings = new TestReadings()
            .Starved("smelter", Items.IronOre, 0.5).Bottleneck("mk1", 30).Starved("plates", Items.IronIngot, 0.5).Into("mk1", Items.IronIngot, 60).Build(world);
        TransportTier[] tiers = [new("Build_ConveyorBeltMk1_C", LogisticsKind.Belt, 60), new("Build_ConveyorBeltMk2_C", LogisticsKind.Belt, 120)];

        TraceStart start = Single(ShortageTrace.From(world, readings, Ids("plates"), tiers));

        Assert.Equal(new MoreCapacity("Build_ConveyorBeltMk2_C", 1, 90), start.MoreCapacity);
    }

    [Fact]
    public void A_starved_producer_is_followed_to_where_its_own_shortage_starts()
    {
        // The miner gives all it can; the smelter gets half its ore; the constructor half its ingots.
        WorldGraph world = new TestWorld()
            .Miner("miner", Items.IronOre, 30).Belt("ore").Machine("smelter", Items.IronOre, 60, Items.IronIngot, 60)
            .Belt("ingots").Machine("plates", Items.IronIngot, 60, Items.IronPlate, 40)
            .Connect("miner", "ore", "smelter", "ingots", "plates").Build();
        FlowReadings readings = new TestReadings()
            .Starved("smelter", Items.IronOre, 0.5).Starved("plates", Items.IronIngot, 0.5).Into("ore", Items.IronOre, 30).Into("ingots", Items.IronIngot, 30).Build(world);

        TraceStart start = Single(ShortageTrace.From(world, readings, Ids("plates")));

        Assert.Equal((TraceCause.SourceAtLimit, Items.IronOre), (start.Cause, start.Item));
        Assert.Equal(Ids("miner"), start.At);
        Assert.Equal(30, start.PerMinute);
        Assert.Equal([(Items.IronIngot, 30.0), (Items.IronOre, 30.0)], start.Path.Select(step => (step.Item, step.PerMinute)));
        Assert.Equal(Ids("smelter"), start.Path[1].Machines);
    }

    [Fact]
    public void Producers_at_full_speed_whose_output_partly_goes_to_other_machines_have_it_taken_by_others()
    {
        WorldGraph world = new TestWorld()
            .Machine("smelter", Items.IronOre, 30, Items.IronIngot, 30).Belt("ingots").Splitter("split")
            .Belt("to plates").Machine("plates", Items.IronIngot, 30, Items.IronPlate, 20)
            .Belt("to rods").Machine("rods", Items.IronIngot, 15, Items.IronRod, 15)
            .Connect("smelter", "ingots", "split", "to plates", "plates").Connect("split", "to rods", "rods").Build();
        FlowReadings readings = new TestReadings()
            .Running("smelter").Starved("plates", Items.IronIngot, 0.5).Running("rods")
            .Into("ingots", Items.IronIngot, 30).Into("to plates", Items.IronIngot, 15).Into("to rods", Items.IronIngot, 15).Build(world);

        TraceStart start = Single(ShortageTrace.From(world, readings, Ids("plates")));

        Assert.Equal(TraceCause.TakenByOthers, start.Cause);
        Assert.Equal(Ids("smelter"), start.At);
        Assert.Equal(new Dictionary<BuildableId, double> { [BuildableId.Of("rods")] = 15 }, start.Others);
    }

    [Fact]
    public void A_line_that_starts_at_nothing_brings_nothing()
    {
        WorldGraph world = new TestWorld().Belt("loose end").Machine("plates", Items.IronIngot, 30, Items.IronPlate, 20).Connect("loose end", "plates").Build();
        FlowReadings readings = new TestReadings().Starved("plates", Items.IronIngot, 0).Build(world);

        TraceStart start = Single(ShortageTrace.From(world, readings, Ids("plates")));

        Assert.Equal(TraceCause.NothingBringsIt, start.Cause);
        Assert.Equal(Ids("loose end"), start.At);
    }

    [Fact]
    public void A_station_whose_supply_the_save_does_not_say_is_unknown_supply()
    {
        WorldGraph world = new TestWorld()
            .Station("station", (Items.IronIngot, 100)).Belt("ingots").Machine("plates", Items.IronIngot, 30, Items.IronPlate, 20)
            .Connect("station", "ingots", "plates").Build();
        FlowReadings readings = new TestReadings().Unknown("station", Items.IronIngot, 15).Starved("plates", Items.IronIngot, 0.5).Into("ingots", Items.IronIngot, 15).Build(world);

        TraceStart start = Single(ShortageTrace.From(world, readings, Ids("plates")));

        Assert.Equal(TraceCause.UnknownSupply, start.Cause);
        Assert.Equal(15, start.PerMinute);
    }

    [Fact]
    public void A_station_that_delivered_only_other_items_onto_a_shared_line_is_not_where_it_starts()
    {
        // The platform unloads ore onto the belt the smelter's ingots also take; the analysis has it deliver no ingots.
        WorldGraph world = new TestWorld()
            .Machine("smelter", Items.IronOre, 30, Items.IronIngot, 30).Station("platform").Merger("merge").Belt("mixed")
            .Machine("plates", Items.IronIngot, 60, Items.IronPlate, 40)
            .Connect("smelter", "merge", "mixed", "plates").Connect("platform", "merge").Build();
        FlowReadings readings = new TestReadings()
            .Running("smelter").Unknown("platform", Items.IronOre, 20).Starved("plates", Items.IronIngot, 0.5).Into("mixed", Items.IronIngot, 30).Build(world);

        TraceStart start = Single(ShortageTrace.From(world, readings, Ids("plates")));

        Assert.Equal(TraceCause.TooFewProducers, start.Cause);
        Assert.Equal(Ids("smelter"), start.At);
    }

    [Fact]
    public void A_producer_whose_output_is_blocked_is_where_it_starts()
    {
        WorldGraph world = new TestWorld()
            .Machine("smelter", Items.IronOre, 30, Items.IronIngot, 30).Belt("ingots").Machine("plates", Items.IronIngot, 30, Items.IronPlate, 20)
            .Connect("smelter", "ingots", "plates").Build();
        FlowReadings readings = new TestReadings().Blocked("smelter", Items.IronIngot, 0.5).Starved("plates", Items.IronIngot, 0.5).Build(world);

        TraceStart start = Single(ShortageTrace.From(world, readings, Ids("plates")));

        Assert.Equal(TraceCause.ProducerOutputBlocked, start.Cause);
        Assert.Equal(Ids("smelter"), start.At);
    }

    [Fact]
    public void Starved_machines_sharing_a_line_are_traced_together_to_one_start()
    {
        WorldGraph world = new TestWorld()
            .Machine("smelter", Items.IronOre, 30, Items.IronIngot, 30).Belt("ingots").Splitter("split")
            .Belt("to A").Machine("plates A", Items.IronIngot, 30, Items.IronPlate, 20)
            .Belt("to B").Machine("plates B", Items.IronIngot, 30, Items.IronPlate, 20)
            .Connect("smelter", "ingots", "split", "to A", "plates A").Connect("split", "to B", "plates B").Build();
        FlowReadings readings = new TestReadings()
            .Running("smelter").Starved("plates A", Items.IronIngot, 0.5).Starved("plates B", Items.IronIngot, 0.5)
            .Into("ingots", Items.IronIngot, 30).Into("to A", Items.IronIngot, 15).Into("to B", Items.IronIngot, 15).Build(world);

        TraceStart start = Single(ShortageTrace.From(world, readings, Ids("plates A", "plates B")));

        Assert.Equal(TraceCause.TooFewProducers, start.Cause);
        Assert.Equal(Ids("plates A", "plates B"), start.Path[0].Machines);
        Assert.Equal(30, start.Path[0].PerMinute);
    }

    [Fact]
    public void Only_the_starved_machines_of_those_asked_about_are_traced()
    {
        WorldGraph world = new TestWorld()
            .Machine("smelter", Items.IronOre, 30, Items.IronIngot, 30).Belt("ingots").Machine("plates", Items.IronIngot, 60, Items.IronPlate, 40)
            .Machine("other plates", Items.IronIngot, 30, Items.IronPlate, 20)
            .Connect("smelter", "ingots", "plates").Build();
        FlowReadings readings = new TestReadings().Running("smelter").Starved("plates", Items.IronIngot, 0.5).Running("other plates").Into("ingots", Items.IronIngot, 30).Build(world);

        TraceStart start = Single(ShortageTrace.From(world, readings, Ids("plates", "other plates")));

        Assert.Equal(Ids("plates"), start.Path[0].Machines);
    }

    [Fact]
    public void A_starved_generator_counts_the_fuel_it_lacks()
    {
        WorldGraph world = new TestWorld()
            .Miner("coal mine", Items.Coal, 7.5m).Belt("coal").Generator("coal plant", Items.Coal, 15)
            .Connect("coal mine", "coal", "coal plant").Build();
        FlowReadings readings = new TestReadings().Starved("coal plant", Items.Coal, 0.5).Into("coal", Items.Coal, 7.5).Build(world);

        TraceStart start = Assert.Single(ShortageTrace.From(world, readings, Ids("coal plant")));

        Assert.Equal(7.5, Assert.Single(start.Path).PerMinute);
    }

    [Fact]
    public void A_generator_that_leaves_none_of_its_waste_is_not_where_the_waste_comes_from()
    {
        // The plant's fuel names a waste, but none of it leaves at its rate; iron plates stand in for what the waste becomes.
        WorldGraph world = new TestWorld()
            .Generator("plant", Items.UraniumFuelRod, 0.2m, Items.Water, 240, Items.UraniumWaste, 0).Belt("waste")
            .Machine("reprocessor", Items.UraniumWaste, 10, Items.IronPlate, 1)
            .Connect("plant", "waste", "reprocessor").Build();
        FlowReadings readings = new TestReadings().Running("plant").Starved("reprocessor", Items.UraniumWaste, 0).Build(world);

        TraceStart start = Single(ShortageTrace.From(world, readings, Ids("reprocessor")));

        Assert.Equal(TraceCause.NothingBringsIt, start.Cause);
        Assert.DoesNotContain(BuildableId.Of("plant"), start.At);
    }

    [Fact]
    public void A_generator_that_leaves_its_waste_is_where_the_waste_comes_from()
    {
        WorldGraph world = new TestWorld()
            .Generator("plant", Items.UraniumFuelRod, 0.2m, Items.Water, 240, Items.UraniumWaste, 10).Belt("waste")
            .Machine("reprocessor", Items.UraniumWaste, 20, Items.IronPlate, 1)
            .Connect("plant", "waste", "reprocessor").Build();
        FlowReadings readings = new TestReadings().Running("plant").Starved("reprocessor", Items.UraniumWaste, 0.5).Into("waste", Items.UraniumWaste, 10).Build(world);

        TraceStart start = Single(ShortageTrace.From(world, readings, Ids("reprocessor")));

        Assert.Equal(TraceCause.TooFewProducers, start.Cause);
        Assert.Equal(Ids("plant"), start.At);
    }
}
