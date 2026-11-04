namespace SatisfactoryLedger.World.Domain.Tests;

/// <summary>Which machines and logistics belong to a factory (BR-W1 to BR-W8).</summary>
public class FactoryMembersTests
{
    private static FactoryMembers Trace(WorldGraph world, string outputBelt, params ItemId[] inputItems) =>
        new FactoryDefinition("Test factory", BuildableId.Of(outputBelt), inputItems).TraceMembers(world);

    /// <summary>miner → ore → smelter → ingots → constructor → output.</summary>
    private static TestWorld IronPlateLine() => new TestWorld()
        .Miner("miner").Belt("ore")
        .Machine("smelter", Items.IronOre, Items.IronIngot).Belt("ingots")
        .Machine("constructor", Items.IronIngot, Items.IronPlate).Belt("output")
        .Connect("miner", "ore", "smelter", "ingots", "constructor", "output");

    [Fact]
    public void A_machine_that_makes_the_output_item_is_part_of_the_factory()
    {
        var members = Trace(IronPlateLine().Build(), "output", Items.IronOre);

        Assert.Contains(BuildableId.Of("constructor"), members.Machines);
    }

    [Fact]
    public void A_machine_that_feeds_a_machine_of_the_factory_is_part_of_the_factory()
    {
        var members = Trace(IronPlateLine().Build(), "output", Items.IronOre);

        Assert.Contains(BuildableId.Of("smelter"), members.Machines);
    }

    [Fact]
    public void A_provider_is_never_part_of_the_factory()
    {
        var members = Trace(IronPlateLine().Build(), "output", Items.IronOre);

        Assert.False(members.Contains(BuildableId.Of("miner")));
    }

    [Fact]
    public void The_output_belt_is_part_of_the_factory()
    {
        var members = Trace(IronPlateLine().Build(), "output", Items.IronOre);

        Assert.Contains(BuildableId.Of("output"), members.Logistics);
    }

    [Fact]
    public void Every_machine_merged_into_the_output_belt_is_part_of_the_factory()
    {
        var world = new TestWorld()
            .Belt("ingots").Splitter("split")
            .Machine("left", Items.IronIngot, Items.IronPlate).Machine("right", Items.IronIngot, Items.IronPlate)
            .Merger("merge").Belt("output")
            .Connect("ingots", "split", "left", "merge", "output")
            .Connect("split", "right", "merge")
            .Build();

        var members = Trace(world, "output", Items.IronIngot);

        Ids.AssertSet(members.Machines, "left", "right");
    }

    [Fact]
    public void The_logistics_between_the_machines_and_the_output_belt_are_part_of_the_factory()
    {
        var world = new TestWorld()
            .Machine("left", Items.IronIngot, Items.IronPlate).Machine("right", Items.IronIngot, Items.IronPlate)
            .Belt("from left").Belt("from right").Merger("merge").Belt("output")
            .Connect("left", "from left", "merge", "output")
            .Connect("right", "from right", "merge")
            .Build();

        var members = Trace(world, "output", Items.IronIngot);

        Ids.AssertSet(members.Logistics, "from left", "from right", "merge", "output");
    }

    [Fact]
    public void A_machine_that_makes_a_declared_input_item_belongs_to_another_factory()
    {
        var world = new TestWorld()
            .Machine("plate constructor", Items.IronIngot, Items.IronPlate).Belt("plates")
            .Machine("assembler", [Items.IronPlate, Items.Screw], [Items.ReinforcedIronPlate]).Belt("output")
            .Connect("plate constructor", "plates", "assembler", "output")
            .Build();

        var members = Trace(world, "output", Items.IronPlate, Items.Screw);

        Assert.DoesNotContain(BuildableId.Of("plate constructor"), members.Machines);
    }

    [Fact]
    public void A_sink_that_takes_the_overflow_of_the_output_is_not_part_of_the_factory()
    {
        var world = new TestWorld()
            .Machine("constructor", Items.IronIngot, Items.IronPlate).Splitter("overflow").Sink("sink").Belt("output")
            .Connect("constructor", "overflow", "output")
            .Connect("overflow", "sink")
            .Build();

        var members = Trace(world, "output", Items.IronIngot);

        Assert.False(members.Contains(BuildableId.Of("sink")));
    }

    [Fact]
    public void An_input_starts_at_the_splitter_that_only_feeds_the_factory()
    {
        // miner → main → first → (left, second → right). "main" is supply; "first" and what follows are the factory's.
        var world = new TestWorld()
            .Miner("miner").Belt("main").Splitter("first").Splitter("second")
            .Machine("left", Items.IronOre, Items.IronIngot).Machine("right", Items.IronOre, Items.IronIngot)
            .Merger("merge").Belt("output")
            .Connect("miner", "main", "first", "left", "merge", "output")
            .Connect("first", "second", "right", "merge")
            .Build();

        var members = Trace(world, "output", Items.IronOre);

        Ids.AssertSet(members.Logistics, "first", "second", "merge", "output");
    }

    [Fact]
    public void A_splitter_that_also_feeds_something_outside_the_factory_is_supply()
    {
        // The main line passes the factory: "tap" sends ore to it and lets the rest continue to another smelter.
        var world = new TestWorld()
            .Miner("miner").Belt("main").Splitter("tap").Belt("main continued")
            .Machine("other smelter", Items.IronOre, Items.IronIngot)
            .Belt("branch").Splitter("first")
            .Machine("left", Items.IronOre, Items.IronIngot).Machine("right", Items.IronOre, Items.IronIngot)
            .Merger("merge").Belt("output")
            .Connect("miner", "main", "tap", "main continued", "other smelter")
            .Connect("tap", "branch", "first", "left", "merge", "output")
            .Connect("first", "right", "merge")
            .Build();

        var members = Trace(world, "output", Items.IronOre);

        Ids.AssertSet(members.Logistics, "first", "merge", "output");
    }

    [Fact]
    public void A_manifold_that_feeds_machines_of_two_factories_is_supply_for_both()
    {
        // main → s1 → (x1, s2); s2 → (x2, continued → y1). x1 and x2 are factory X, y1 is factory Y.
        var world = new TestWorld()
            .Miner("miner").Belt("main").Splitter("s1").Splitter("s2").Belt("continued")
            .Machine("x1", Items.IronOre, Items.IronIngot).Machine("x2", Items.IronOre, Items.IronIngot)
            .Machine("y1", Items.IronOre, Items.IronIngot)
            .Merger("merge").Belt("x output").Belt("y output")
            .Connect("miner", "main", "s1", "x1", "merge", "x output")
            .Connect("s1", "s2", "x2", "merge")
            .Connect("s2", "continued", "y1", "y output")
            .Build();

        var factoryX = Trace(world, "x output", Items.IronOre);
        var factoryY = Trace(world, "y output", Items.IronOre);

        Ids.AssertSet(factoryX.Logistics, "merge", "x output");
        Ids.AssertSet(factoryY.Logistics, "y output");
    }

    [Fact]
    public void A_machine_can_be_part_of_two_factories()
    {
        var world = new TestWorld()
            .Machine("smelter", Items.IronOre, Items.IronIngot).Splitter("split")
            .Machine("plates", Items.IronIngot, Items.IronPlate).Belt("plate output")
            .Machine("rods", Items.IronIngot, Items.IronRod).Belt("rod output")
            .Connect("smelter", "split", "plates", "plate output")
            .Connect("split", "rods", "rod output")
            .Build();

        var plateFactory = Trace(world, "plate output", Items.IronOre);
        var rodFactory = Trace(world, "rod output", Items.IronOre);

        Assert.Contains(BuildableId.Of("smelter"), plateFactory.Machines);
        Assert.Contains(BuildableId.Of("smelter"), rodFactory.Machines);
    }

    [Fact]
    public void A_splitter_after_a_shared_machine_is_part_of_both_factories()
    {
        var world = new TestWorld()
            .Machine("smelter", Items.IronOre, Items.IronIngot).Splitter("split")
            .Machine("plates", Items.IronIngot, Items.IronPlate).Belt("plate output")
            .Machine("rods", Items.IronIngot, Items.IronRod).Belt("rod output")
            .Connect("smelter", "split", "plates", "plate output")
            .Connect("split", "rods", "rod output")
            .Build();

        var plateFactory = Trace(world, "plate output", Items.IronOre);
        var rodFactory = Trace(world, "rod output", Items.IronOre);

        Assert.Contains(BuildableId.Of("split"), plateFactory.Logistics);
        Assert.Contains(BuildableId.Of("split"), rodFactory.Logistics);
    }

    [Fact]
    public void A_loop_between_machines_is_followed_once()
    {
        // The second machine sends part of its output back to the first.
        var world = new TestWorld()
            .Machine("first", Items.IronOre, Items.IronIngot).Belt("forward")
            .Machine("second", Items.IronIngot, Items.IronPlate).Splitter("split").Belt("back").Belt("output")
            .Connect("first", "forward", "second", "split", "output")
            .Connect("split", "back", "first")
            .Build();

        var members = Trace(world, "output", Items.IronOre);

        Ids.AssertSet(members.Machines, "first", "second");
    }

    [Fact]
    public void A_lift_can_be_the_output_belt()
    {
        var world = new TestWorld()
            .Machine("constructor", Items.IronIngot, Items.IronPlate).Lift("lift")
            .Connect("constructor", "lift")
            .Build();

        var members = Trace(world, "lift", Items.IronIngot);

        Assert.Contains(BuildableId.Of("constructor"), members.Machines);
    }

    [Fact]
    public void The_output_belt_must_be_a_belt()
    {
        var world = IronPlateLine().Build();

        Assert.Throws<ArgumentException>(() => Trace(world, "constructor", Items.IronOre));
    }

    [Fact]
    public void The_output_belt_must_be_in_the_world()
    {
        var world = IronPlateLine().Build();

        Assert.Throws<ArgumentException>(() => Trace(world, "nowhere", Items.IronOre));
    }

    [Fact]
    public void A_factory_has_a_name()
    {
        Assert.Throws<ArgumentException>(() => new FactoryDefinition(" ", BuildableId.Of("output"), [Items.IronOre]));
    }
}
