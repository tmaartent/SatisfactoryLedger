using SatisfactoryLedger.Catalog.Contracts;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.SaveFile;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Tests;

/// <summary>The stored form of a snapshot (ADR-0016): what goes in, what comes back out, and BR-W10.</summary>
public class SnapshotDocumentTests
{
    private static readonly SavedWorld TestWorld = ReadTestWorld();

    private static SavedWorld ReadTestWorld()
    {
        using FileStream file = File.OpenRead("Fixtures/Development.sav");
        return SaveFileReader.Read(file);
    }

    private static BuildableId Id(string name) => BuildableId.Of($"Persistent_Level:PersistentLevel.{name}");

    private static readonly CatalogRecipe IngotIron =
        new("Recipe_IngotIron_C", "Build_SmelterMk1_C", 2, [new("Desc_OreIron_C", 1)], [new("Desc_IronIngot_C", 1)]);

    private static readonly CatalogRecipe IronPlate =
        new("Recipe_IronPlate_C", "Build_ConstructorMk1_C", 6, [new("Desc_IronIngot_C", 3)], [new("Desc_IronPlate_C", 2)]);

    private static readonly CatalogRecipe IronRod =
        new("Recipe_IronRod_C", "Build_ConstructorMk1_C", 4, [new("Desc_IronIngot_C", 1)], [new("Desc_IronRod_C", 1)]);

    private static readonly CatalogRecipe PackagedWater =
        new("Recipe_PackagedWater_C", "Build_Packager_C", 2, [new("Desc_Water_C", 2), new("Desc_FluidCanister_C", 2)], [new("Desc_PackagedWater_C", 2)]);

    private static readonly CatalogRecipe Screw =
        new("Recipe_Screw_C", "Build_ConstructorMk1_C", 6, [new("Desc_IronRod_C", 1)], [new("Desc_IronScrew_C", 4)]);

    /// <summary>Stored and read back, as the database does with it.</summary>
    private static SnapshotDocument Stored(params CatalogRecipe[] catalog) =>
        SnapshotDocument.FromJson(SnapshotDocument.Create(TestWorld, catalog).ToJson());

    [Fact]
    public void A_stored_snapshot_keeps_every_buildable_and_connection_of_the_save()
    {
        SnapshotDocument snapshot = Stored(IngotIron, IronPlate, IronRod, PackagedWater);

        Assert.Equal(new SnapshotCounts(Machines: 5, Providers: 2, Consumers: 1, Logistics: 22, Connections: 28), snapshot.Counts());
    }

    [Fact]
    public void The_iron_plate_factory_can_be_traced_in_a_stored_snapshot()
    {
        WorldGraph world = Stored(IngotIron, IronPlate, IronRod, PackagedWater).ToWorldGraph();

        FactoryMembers members = new FactoryDefinition("Iron plates", Id("Build_ConveyorBeltMk1_C_2147276358"), [ItemId.Of("Desc_OreIron_C")])
            .TraceMembers(world);

        Assert.Equal(
            new HashSet<BuildableId> { Id("Build_SmelterMk1_C_2147301226"), Id("Build_SmelterMk1_C_2147301500"), Id("Build_ConstructorMk1_C_2147291547") },
            members.Machines.ToHashSet());
    }

    [Fact]
    public void A_snapshot_copies_only_the_recipes_its_machines_use()
    {
        SnapshotDocument snapshot = Stored(IngotIron, IronPlate, IronRod, PackagedWater, Screw);

        Assert.Equal(
            ["Recipe_IngotIron_C", "Recipe_IronPlate_C", "Recipe_IronRod_C", "Recipe_PackagedWater_C"],
            snapshot.Recipes.Select(recipe => recipe.Id).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_recipe_the_catalog_does_not_know_is_listed_as_unknown()
    {
        SnapshotDocument snapshot = Stored(IngotIron, IronPlate, IronRod);

        Assert.Equal(["Recipe_PackagedWater_C"], snapshot.UnknownRecipes);
    }

    [Fact]
    public void A_machine_with_an_unknown_recipe_stays_in_the_world_without_known_inputs_or_outputs()
    {
        WorldGraph world = Stored(IngotIron, IronPlate, IronRod).ToWorldGraph();

        Machine packager = Assert.IsType<Machine>(world.Find(Id("Build_Packager_C_2147260704")));
        Assert.Empty(packager.Consumes);
        Assert.Empty(packager.Produces);
    }

    [Fact]
    public void A_machine_with_a_known_recipe_consumes_and_produces_its_items()
    {
        WorldGraph world = Stored(IngotIron, IronPlate, IronRod, PackagedWater).ToWorldGraph();

        Machine constructorC = Assert.IsType<Machine>(world.Find(Id("Build_ConstructorMk1_C_2147291547")));
        Assert.Equal([ItemId.Of("Desc_IronIngot_C")], constructorC.Consumes);
        Assert.Equal([ItemId.Of("Desc_IronPlate_C")], constructorC.Produces);
    }

    [Fact]
    public void Clock_speeds_and_nodes_are_kept_exactly()
    {
        SnapshotDocument snapshot = Stored(IngotIron, IronPlate, IronRod, PackagedWater);

        Assert.Equal(33.3333m, snapshot.Buildables.Single(b => b.Id == Id("Build_SmelterMk1_C_2147301500").Id).ClockPercent);
        Assert.Equal("BP_ResourceNode35", snapshot.Buildables.Single(b => b.Id == Id("Build_MinerMk1_C_2147339296").Id).Node);
    }

    [Fact]
    public void A_snapshot_document_says_which_version_of_the_format_it_is()
    {
        Assert.Equal(1, Stored().Version);
    }
}
