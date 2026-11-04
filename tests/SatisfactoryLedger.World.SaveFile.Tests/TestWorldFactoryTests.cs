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

    private static readonly Dictionary<string, (ItemId[] Consumes, ItemId[] Produces)> Recipes = new()
    {
        ["Recipe_IngotIron_C"] = ([IronOre], [IronIngot]),
        ["Recipe_IronPlate_C"] = ([IronIngot], [ItemId.Of("Desc_IronPlate_C")]),
        ["Recipe_IronRod_C"] = ([IronIngot], [ItemId.Of("Desc_IronRod_C")]),
        ["Recipe_PackagedWater_C"] = ([ItemId.Of("Desc_Water_C"), ItemId.Of("Desc_FluidCanister_C")], [ItemId.Of("Desc_PackagedWater_C")]),
    };

    private static WorldGraph Graph() => new(
        Save.Buildables.Select<SavedBuildable, Buildable>(saved => saved switch
        {
            SavedMachine machine => new Machine(machine.Id, Recipes[machine.Recipe!].Consumes, Recipes[machine.Recipe!].Produces),
            SavedProvider provider => new Provider(provider.Id),
            SavedConsumer consumer => new Consumer(consumer.Id),
            SavedLogistics logistics => new Logistics(logistics.Id, logistics.Kind),
            _ => throw new InvalidOperationException($"Unexpected {saved}"),
        }),
        Save.Connections);

    [Fact]
    public void The_iron_plate_factory_traced_from_the_belt_into_the_container_has_both_smelters_and_constructor_C()
    {
        var members = new FactoryDefinition("Iron plates", BeltIntoContainer, [IronOre]).TraceMembers(Graph());

        Assert.Equal(new HashSet<BuildableId> { SmelterA, SmelterB, ConstructorC }, members.Machines.ToHashSet());
    }

    [Fact]
    public void The_iron_plate_factory_starts_at_the_splitter_and_ends_at_the_belt_into_the_container()
    {
        var members = new FactoryDefinition("Iron plates", BeltIntoContainer, [IronOre]).TraceMembers(Graph());

        Assert.Contains(Splitter, members.Logistics);
        Assert.Contains(SmartSplitter, members.Logistics);
        Assert.Contains(BeltIntoContainer, members.Logistics);
        Assert.DoesNotContain(Lift, members.Logistics);
        Assert.DoesNotContain(FirstBelt, members.Logistics);
    }

    [Fact]
    public void The_overflow_sink_is_not_part_of_the_iron_plate_factory()
    {
        var members = new FactoryDefinition("Iron plates", BeltIntoContainer, [IronOre]).TraceMembers(Graph());

        Assert.False(members.Contains(Sink));
    }
}
