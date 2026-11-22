using SatisfactoryLedger.World.Domain.Tracing;

namespace SatisfactoryLedger.World.Domain.Tests.Tracing;

/// <summary>
/// Where an output blockage starts (decided 2026-11-20), the mirror of a shortage: from output-blocked machines,
/// downstream along the item that can't leave, through consumers that are blocked themselves, to the first place that
/// takes no more for a reason of its own.
/// </summary>
public class BlockageTraceTests
{
    private static BuildableId[] Ids(params string[] ids) => [.. ids.Select(BuildableId.Of)];

    [Fact]
    public void Consumers_running_at_full_speed_on_all_it_sends_are_too_few()
    {
        // The smelter could make 60 ingots; the constructor takes 30 at full speed.
        WorldGraph world = new TestWorld()
            .Machine("smelter", Items.IronOre, 60, Items.IronIngot, 60).Belt("ingots").Machine("plates", Items.IronIngot, 30, Items.IronPlate, 20)
            .Connect("smelter", "ingots", "plates").Build();
        FlowReadings readings = new TestReadings().Blocked("smelter", Items.IronIngot, 0.5).Running("plates").Into("ingots", Items.IronIngot, 30).Build();

        TraceStart start = Assert.Single(BlockageTrace.From(world, readings, Ids("smelter")));

        Assert.Equal((TraceCause.TooFewConsumers, Items.IronIngot), (start.Cause, start.Item));
        Assert.Equal(Ids("plates"), start.At);
        Assert.Equal(30, start.PerMinute);
        Assert.Equal(30, Assert.Single(start.Path).PerMinute);
    }

    [Fact]
    public void A_consumer_starved_of_another_input_takes_no_more()
    {
        WorldGraph world = new TestWorld()
            .Machine("plates", Items.IronIngot, 30, Items.IronPlate, 20).Belt("to assembler")
            .Machine("assembler", [Items.IronPlate, Items.Screw], [Items.ReinforcedIronPlate])
            .Connect("plates", "to assembler", "assembler").Build();
        FlowReadings readings = new TestReadings().Blocked("plates", Items.IronPlate, 0.5).Starved("assembler", Items.Screw, 0.25).Build();

        TraceStart start = Assert.Single(BlockageTrace.From(world, readings, Ids("plates")));

        Assert.Equal(TraceCause.ConsumerStarved, start.Cause);
        Assert.Equal(Ids("assembler"), start.At);
        Assert.Equal(new Dictionary<ItemId, double> { [Items.Screw] = 0.25 }, start.ShortOf);
    }

    [Fact]
    public void A_full_belt_on_the_way_is_where_it_starts()
    {
        WorldGraph world = new TestWorld()
            .Machine("smelter", Items.IronOre, 120, Items.IronIngot, 120).Belt("mk1", 60).Machine("plates", Items.IronIngot, 120, Items.IronPlate, 80)
            .Connect("smelter", "mk1", "plates").Build();
        FlowReadings readings = new TestReadings().Blocked("smelter", Items.IronIngot, 0.5).Bottleneck("mk1", 60).Starved("plates", Items.IronIngot, 0.5).Into("mk1", Items.IronIngot, 60).Build();

        TraceStart start = Assert.Single(BlockageTrace.From(world, readings, Ids("smelter")));

        Assert.Equal(TraceCause.BeltFull, start.Cause);
        Assert.Equal(Ids("mk1"), start.At);
    }

    [Fact]
    public void A_full_belt_at_the_highest_tier_needs_more_lines_of_it()
    {
        WorldGraph world = new TestWorld()
            .Machine("smelter", Items.IronOre, 120, Items.IronIngot, 120).Belt("mk1", 60).Machine("plates", Items.IronIngot, 120, Items.IronPlate, 80)
            .Connect("smelter", "mk1", "plates").Build();
        FlowReadings readings = new TestReadings().Blocked("smelter", Items.IronIngot, 0.5).Bottleneck("mk1", 60).Starved("plates", Items.IronIngot, 0.5).Into("mk1", Items.IronIngot, 60).Build();

        TraceStart start = Assert.Single(BlockageTrace.From(world, readings, Ids("smelter"), [new("Build_ConveyorBeltMk1_C", LogisticsKind.Belt, 60)]));

        Assert.Equal(new MoreCapacity("Build_ConveyorBeltMk1_C", 2, 120), start.MoreCapacity);
    }

    [Fact]
    public void A_belt_that_ends_at_nothing_takes_nothing()
    {
        WorldGraph world = new TestWorld().Machine("smelter", Items.IronOre, 30, Items.IronIngot, 30).Belt("loose end").Connect("smelter", "loose end").Build();
        FlowReadings readings = new TestReadings().Blocked("smelter", Items.IronIngot, 0).Build();

        TraceStart start = Assert.Single(BlockageTrace.From(world, readings, Ids("smelter")));

        Assert.Equal(TraceCause.NowhereToGo, start.Cause);
        Assert.Equal(Ids("loose end"), start.At);
    }

    [Fact]
    public void A_machine_with_no_line_for_the_item_it_cannot_get_away_is_where_it_starts()
    {
        // The blender's tanks have a belt; its gas has no pipe that carries it (decided 2026-11-22, Nitro Rocket Fuel).
        WorldGraph world = new TestWorld()
            .Machine("blender", [Items.Coal, Items.Water], [Items.NitrogenGas, Items.EmptyFluidTank]).BeltCarrying("tanks", (Items.EmptyFluidTank, 10))
            .Machine("packager", Items.EmptyFluidTank, Items.PackagedNitrogenGas)
            .Connect("blender", "tanks", "packager").Build();
        FlowReadings readings = new TestReadings().Blocked("blender", Items.NitrogenGas, 0).Running("packager").Build();

        TraceStart start = Assert.Single(BlockageTrace.From(world, readings, Ids("blender")));

        Assert.Equal((TraceCause.NowhereToGo, Items.NitrogenGas), (start.Cause, start.Item));
        Assert.Equal(Ids("blender"), start.At);
    }

    [Fact]
    public void A_machine_with_no_line_out_at_all_is_where_it_starts()
    {
        WorldGraph world = new TestWorld().Machine("smelter", Items.IronOre, 30, Items.IronIngot, 30).Build();
        FlowReadings readings = new TestReadings().Blocked("smelter", Items.IronIngot, 0).Build();

        TraceStart start = Assert.Single(BlockageTrace.From(world, readings, Ids("smelter")));

        Assert.Equal(TraceCause.NowhereToGo, start.Cause);
        Assert.Equal(Ids("smelter"), start.At);
    }

    [Fact]
    public void Blocked_machines_the_trace_finds_no_start_for_are_unclear_not_left_out()
    {
        // The box still has room, so nothing on the way explains the blockage the readings report.
        Inventory room = Inventory.OfSlots(1, new Dictionary<ItemId, decimal> { [Items.IronIngot] = 10 }, new Dictionary<ItemId, int> { [Items.IronIngot] = 100 });
        WorldGraph world = new TestWorld()
            .Machine("smelter", Items.IronOre, 30, Items.IronIngot, 30).Belt("ingots").Container("box", room)
            .Connect("smelter", "ingots", "box").Build();
        FlowReadings readings = new TestReadings().Blocked("smelter", Items.IronIngot, 0).Build();

        TraceStart start = Assert.Single(BlockageTrace.From(world, readings, Ids("smelter")));

        Assert.Equal((TraceCause.Unclear, Items.IronIngot), (start.Cause, start.Item));
        Assert.Equal(Ids("smelter"), start.At);
        Assert.Equal(Ids("smelter"), Assert.Single(start.Path).Machines);
    }

    [Fact]
    public void A_full_container_at_the_end_of_the_line_is_where_it_starts()
    {
        Inventory full = Inventory.OfSlots(1, new Dictionary<ItemId, decimal> { [Items.IronIngot] = 100 }, new Dictionary<ItemId, int> { [Items.IronIngot] = 100 });
        WorldGraph world = new TestWorld()
            .Machine("smelter", Items.IronOre, 30, Items.IronIngot, 30).Belt("ingots").Container("box", full)
            .Connect("smelter", "ingots", "box").Build();
        FlowReadings readings = new TestReadings().Blocked("smelter", Items.IronIngot, 0).Build();

        TraceStart start = Assert.Single(BlockageTrace.From(world, readings, Ids("smelter")));

        Assert.Equal(TraceCause.StorageFull, start.Cause);
        Assert.Equal(Ids("box"), start.At);
    }

    [Fact]
    public void A_blocked_consumer_is_followed_to_where_its_own_blockage_starts()
    {
        WorldGraph world = new TestWorld()
            .Machine("smelter", Items.IronOre, 60, Items.IronIngot, 60).Belt("ingots")
            .Machine("plates", Items.IronIngot, 60, Items.IronPlate, 40).Belt("to rods")
            .Machine("assembler", Items.IronPlate, 20, Items.ReinforcedIronPlate, 5)
            .Connect("smelter", "ingots", "plates", "to rods", "assembler").Build();
        FlowReadings readings = new TestReadings()
            .Blocked("smelter", Items.IronIngot, 0.5).Blocked("plates", Items.IronPlate, 0.5).Running("assembler")
            .Into("ingots", Items.IronIngot, 30).Into("to rods", Items.IronPlate, 20).Build();

        TraceStart start = Assert.Single(BlockageTrace.From(world, readings, Ids("smelter")));

        Assert.Equal((TraceCause.TooFewConsumers, Items.IronPlate), (start.Cause, start.Item));
        Assert.Equal(Ids("assembler"), start.At);
        Assert.Equal([Items.IronIngot, Items.IronPlate], start.Path.Select(step => step.Item));
    }

    [Fact]
    public void Consumers_at_full_speed_that_also_take_from_other_producers_are_supplied_by_others()
    {
        WorldGraph world = new TestWorld()
            .Machine("smelter A", Items.IronOre, 60, Items.IronIngot, 60).Belt("from A")
            .Machine("smelter B", Items.IronOre, 60, Items.IronIngot, 60).Belt("from B")
            .Merger("merge").Belt("ingots").Machine("plates", Items.IronIngot, 60, Items.IronPlate, 40)
            .Connect("smelter A", "from A", "merge", "ingots", "plates").Connect("smelter B", "from B", "merge").Build();
        FlowReadings readings = new TestReadings()
            .Blocked("smelter A", Items.IronIngot, 1.0 / 6).Running("smelter B").Running("plates")
            .Into("from A", Items.IronIngot, 10).Into("from B", Items.IronIngot, 50).Into("ingots", Items.IronIngot, 60).Build();

        TraceStart start = Assert.Single(BlockageTrace.From(world, readings, Ids("smelter A")));

        Assert.Equal(TraceCause.SuppliedByOthers, start.Cause);
        Assert.Equal(Ids("plates"), start.At);
        Assert.Equal(new Dictionary<BuildableId, double> { [BuildableId.Of("smelter B")] = 50 }, start.Others);
    }
}
