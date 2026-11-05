using SatisfactoryLedger.Catalog.Contracts;
using SatisfactoryLedger.SharedKernel;
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
    private static SnapshotDocument Stored(params CatalogRecipe[] recipes) =>
        TestWorldSnapshot.Stored(TestWorldSnapshot.Catalog with { Recipes = recipes });

    private static T Find<T>(WorldGraph world, string name) where T : Buildable => Assert.IsType<T>(world.Find(Id(name)));

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
    public void Positions_are_kept_so_a_player_can_find_a_buildable_in_the_game()
    {
        PositionDocument position = Stored().Buildables.Single(b => b.Id == Id("Build_MinerMk1_C_2147339296").Id).Position!;

        Assert.Equal(256611.66, position.X, tolerance: 0.01);
    }

    [Fact]
    public void A_snapshot_document_says_which_version_of_the_format_it_is()
    {
        Assert.Equal(2, Stored().Version);
    }

    [Fact]
    public void A_snapshot_copies_only_the_extractors_belts_and_nodes_it_uses()
    {
        SnapshotDocument snapshot = TestWorldSnapshot.Document;

        Assert.Equal(["Build_MinerMk1_C", "Build_WaterPump_C"], snapshot.Extractors.Select(extractor => extractor.Id).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(snapshot.TransportTiers, tier => tier.Id == "Build_ConveyorBeltMk2_C");
        Assert.Equal(["BP_ResourceNode35"], snapshot.ResourceNodes.Select(node => node.Name));
    }

    [Fact]
    public void A_miner_offers_the_ore_of_its_node_at_its_extraction_rate()
    {
        Provider miner = Find<Provider>(TestWorldSnapshot.Document.ToWorldGraph(), "Build_MinerMk1_C_2147339296");

        Assert.Equal(new ItemOffer(ItemId.Of("Desc_OreIron_C"), Rate.PerMinute(60)), miner.Offer);
    }

    [Fact]
    public void A_water_extractor_offers_water_at_its_extraction_rate()
    {
        Provider extractor = Find<Provider>(TestWorldSnapshot.Document.ToWorldGraph(), "Build_WaterPump_C_2147271168");

        Assert.Equal(new ItemOffer(ItemId.Of("Desc_Water_C"), Rate.PerMinute(120)), extractor.Offer);
    }

    [Fact]
    public void A_miner_on_a_node_the_catalog_does_not_know_has_no_known_offer()
    {
        SnapshotDocument snapshot = TestWorldSnapshot.Stored(TestWorldSnapshot.Catalog with { ResourceNodes = [] });

        Assert.Null(Find<Provider>(snapshot.ToWorldGraph(), "Build_MinerMk1_C_2147339296").Offer);
    }

    [Fact]
    public void A_belt_carries_the_capacity_of_its_tier()
    {
        Logistics belt = Find<Logistics>(TestWorldSnapshot.Document.ToWorldGraph(), "Build_ConveyorBeltMk1_C_2147276358");

        Assert.Equal(Rate.PerMinute(60), belt.Capacity);
    }

    [Fact]
    public void A_machine_asks_its_recipe_rate_per_minute_times_its_clock_speed()
    {
        Machine smelterB = Find<Machine>(TestWorldSnapshot.Document.ToWorldGraph(), "Build_SmelterMk1_C_2147301500");

        Assert.Equal(Rate.PerMinute(30) * ClockSpeed.Percent(33.3333m), smelterB.Demand[ItemId.Of("Desc_OreIron_C")]);
    }

    [Fact]
    public void A_snapshot_knows_which_of_the_items_it_uses_are_fluids()
    {
        // Crude oil is a fluid too, but nothing in the test world uses it.
        Assert.Equal(["Desc_Water_C"], TestWorldSnapshot.Document.Fluids);
    }

    [Fact]
    public void A_snapshot_imported_before_rates_were_copied_can_still_be_read_and_traced()
    {
        string version1 = TestWorldSnapshot.Document.ToJson()
            .Replace("\"version\":2", "\"version\":1", StringComparison.Ordinal);

        WorldGraph world = SnapshotDocument.FromJson(version1).ToWorldGraph();

        Assert.Null(Find<Provider>(world, "Build_MinerMk1_C_2147339296").Offer);
    }
}
