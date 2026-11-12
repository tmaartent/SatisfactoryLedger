using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Domain;

using static SatisfactoryLedger.World.SaveFile.Tests.TestWorldSave;

namespace SatisfactoryLedger.World.SaveFile.Tests;

/// <summary>
/// End to end: the test world, read by the adapter, turned into a world graph, and a factory traced in it
/// (BR-W3, BR-W4). The recipes are written out here; at import the Catalog provides them.
/// </summary>
public class TestWorldFactoryTests
{
    private static readonly ItemId IronOre = ItemId.Of("Desc_OreIron_C");
    private static readonly ItemId IronIngot = ItemId.Of("Desc_IronIngot_C");
    private static readonly ItemId Water = ItemId.Of("Desc_Water_C");

    private static readonly Dictionary<string, (ItemId[] Consumes, ItemId[] Produces)> Recipes = new()
    {
        ["Recipe_IngotIron_C"] = ([IronOre], [IronIngot]),
        ["Recipe_IronPlate_C"] = ([IronIngot], [ItemId.Of("Desc_IronPlate_C")]),
        ["Recipe_IronRod_C"] = ([IronIngot], [ItemId.Of("Desc_IronRod_C")]),
        ["Recipe_PackagedWater_C"] = ([Water, ItemId.Of("Desc_FluidCanister_C")], [ItemId.Of("Desc_PackagedWater_C")]),
    };

    private static WorldGraph Graph() => new(
        Save.Buildables.Select<SavedBuildable, Buildable>(saved => saved switch
        {
            SavedMachine machine => new Machine(machine.Id, Traced(Recipes[machine.Recipe!]), machine.ClockSpeed),
            SavedProvider provider => new Provider(provider.Id),
            SavedConsumer consumer => new Consumer(consumer.Id),
            SavedLogistics logistics => new Logistics(logistics.Id, logistics.Kind),
            _ => throw new InvalidOperationException($"Unexpected {saved}"),
        }),
        Save.Connections);

    /// <summary>Tracing only looks at which items a machine takes and makes, so every rate is 1 per minute.</summary>
    private static Recipe Traced((ItemId[] Consumes, ItemId[] Produces) recipe) => new(
        recipe.Consumes.ToDictionary(item => item, _ => Rate.PerMinute(1)),
        recipe.Produces.ToDictionary(item => item, _ => Rate.PerMinute(1)));

    private static readonly BuildableId[] IronPlateMachines = [SmelterA, SmelterB, ConstructorC];

    private static FactoryMembers IronPlates(WorldGraph world) => new FactoryDefinition(IronPlateMachines).TraceMembers(world, new HashSet<ItemId> { Water });

    [Fact]
    public void The_iron_plate_factory_owns_its_supply_line_from_the_miner_since_it_feeds_nothing_else()
    {
        FactoryMembers members = IronPlates(Graph());

        Assert.Superset(new HashSet<BuildableId> { FirstBelt, Lift, Splitter }, members.Logistics.ToHashSet());
        FactoryConnection input = Assert.Single(members.Inputs);
        Assert.Equal((FirstBelt, (BuildableId?)Miner), (input.Inside, input.Outside));
        Assert.Equal([IronOre], input.Items);
    }

    [Fact]
    public void The_iron_plate_factory_hands_its_plates_to_the_sink_and_to_the_container_at_the_end_of_its_line()
    {
        WorldGraph world = Graph();
        BuildableId container = world.Downstream(BeltIntoContainer).Single().Id;
        BuildableId beltIntoSink = world.Upstream(Sink).Single().Id;

        FactoryMembers members = IronPlates(world);

        Assert.Equal(
            new HashSet<(BuildableId, BuildableId?)> { (beltIntoSink, Sink), (container, null) },
            members.Outputs.Select(output => (output.Inside, output.Outside)).ToHashSet());
        Assert.All(members.Outputs, output => Assert.Equal([ItemId.Of("Desc_IronPlate_C")], output.Items));
    }

    [Fact]
    public void The_overflow_sink_is_not_part_of_the_iron_plate_factory()
    {
        Assert.False(IronPlates(Graph()).Contains(Sink));
    }
}
