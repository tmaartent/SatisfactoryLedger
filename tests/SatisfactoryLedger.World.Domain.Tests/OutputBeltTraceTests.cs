namespace SatisfactoryLedger.World.Domain.Tests;

/// <summary>
/// The old way of finding a factory: upstream from its output belt to its input items. Only used to convert factories
/// defined that way into their machines (BR-W2).
/// </summary>
public class OutputBeltTraceTests
{
    private static IReadOnlySet<BuildableId> Trace(WorldGraph world, string outputBelt, params ItemId[] inputItems) =>
        new OutputBeltTrace(BuildableId.Of(outputBelt), inputItems).Machines(world);

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

        Assert.Contains(BuildableId.Of("constructor"), members);
    }

    [Fact]
    public void A_machine_that_feeds_a_machine_of_the_factory_is_part_of_the_factory()
    {
        var members = Trace(IronPlateLine().Build(), "output", Items.IronOre);

        Assert.Contains(BuildableId.Of("smelter"), members);
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

        Ids.AssertSet(members, "left", "right");
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

        Assert.DoesNotContain(BuildableId.Of("plate constructor"), members);
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

        Assert.Contains(BuildableId.Of("smelter"), plateFactory);
        Assert.Contains(BuildableId.Of("smelter"), rodFactory);
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

        Ids.AssertSet(members, "first", "second");
    }

    [Fact]
    public void A_lift_can_be_the_output_belt()
    {
        var world = new TestWorld()
            .Machine("constructor", Items.IronIngot, Items.IronPlate).Lift("lift")
            .Connect("constructor", "lift")
            .Build();

        var members = Trace(world, "lift", Items.IronIngot);

        Assert.Contains(BuildableId.Of("constructor"), members);
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
    public void A_factory_can_be_traced_in_a_world_that_has_its_output_belt()
    {
        var factory = new OutputBeltTrace(BuildableId.Of("output"), [Items.IronOre]);

        Assert.True(factory.IsTraceableIn(IronPlateLine().Build()));
    }

    [Fact]
    public void A_factory_cannot_be_traced_in_a_world_without_its_output_belt()
    {
        // BR-W11: an earlier or later snapshot of the world, in which the belt isn't (or no longer) built.
        var factory = new OutputBeltTrace(BuildableId.Of("rebuilt belt"), [Items.IronOre]);

        Assert.False(factory.IsTraceableIn(IronPlateLine().Build()));
    }

    [Fact]
    public void A_factory_cannot_be_traced_from_something_that_is_not_a_belt()
    {
        var factory = new OutputBeltTrace(BuildableId.Of("constructor"), [Items.IronOre]);

        Assert.False(factory.IsTraceableIn(IronPlateLine().Build()));
    }

}
