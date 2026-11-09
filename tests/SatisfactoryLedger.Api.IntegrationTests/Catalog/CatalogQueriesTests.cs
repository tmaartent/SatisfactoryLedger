using Microsoft.Extensions.DependencyInjection;

// This namespace has its own CatalogRecipe (the API's JSON), and types in the current namespace win over
// `using` imports. An alias makes clear these are the contract's types.
using Contracts = SatisfactoryLedger.Catalog.Contracts;

namespace SatisfactoryLedger.Api.IntegrationTests.Catalog;

/// <summary>What other modules ask the Catalog (Catalog.Contracts), against a real catalog in PostgreSQL.</summary>
public sealed class CatalogQueriesTests(LedgerApiFactory api) : IAsyncLifetime
{
    private readonly HttpClient _client = api.CreateClient();

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        await _client.ImportAsync(CatalogClient.GameFile);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<IReadOnlyList<Contracts.CatalogRecipe>> GetRecipesAsync(params string[] ids)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<Contracts.ICatalogQueries>();
        return await catalog.GetRecipesAsync(ids, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_recipe_is_found_by_its_class_name_with_its_inputs_outputs_and_cycle_time()
    {
        Contracts.CatalogRecipe ironPlate = Assert.Single(await GetRecipesAsync("Recipe_IronPlate_C"));

        Assert.Equal("Build_ConstructorMk1_C", ironPlate.BuildingId);
        Assert.Equal(6m, ironPlate.CycleSeconds);
        Assert.Equal([new Contracts.CatalogIngredient("Desc_IronIngot_C", 3)], ironPlate.Inputs);
        Assert.Equal([new Contracts.CatalogIngredient("Desc_IronPlate_C", 2)], ironPlate.Outputs);
    }

    [Fact]
    public async Task A_recipe_the_catalog_does_not_know_is_left_out()
    {
        IReadOnlyList<Contracts.CatalogRecipe> recipes = await GetRecipesAsync("Recipe_IronPlate_C", "Recipe_FromAFuturePatch_C");

        Assert.Equal(["Recipe_IronPlate_C"], recipes.Select(recipe => recipe.Id));
    }

    [Fact]
    public async Task Items_are_found_by_class_name_and_unknown_ones_are_left_out()
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<Contracts.ICatalogQueries>();

        IReadOnlyList<Contracts.CatalogItem> items = await catalog.GetItemsAsync(
            ["Desc_OreIron_C", "Desc_IronOreTypo_C"], TestContext.Current.CancellationToken);

        Assert.Equal([new Contracts.CatalogItem("Desc_OreIron_C", "Iron Ore", IsFluid: false)], items);
    }

    [Fact]
    public async Task An_item_says_whether_it_is_a_fluid()
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<Contracts.ICatalogQueries>();

        Contracts.CatalogItem water = Assert.Single(await catalog.GetItemsAsync(["Desc_Water_C"], TestContext.Current.CancellationToken));

        Assert.True(water.IsFluid);
    }

    [Fact]
    public async Task Resource_nodes_are_found_by_name_and_unknown_ones_are_left_out()
    {
        await _client.ImportResourceNodesAsync();
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<Contracts.ICatalogQueries>();

        IReadOnlyList<Contracts.CatalogResourceNode> nodes = await catalog.GetResourceNodesAsync(
            ["BP_ResourceNode35", "FGWaterVolume30_0"], TestContext.Current.CancellationToken);

        Assert.Equal([new Contracts.CatalogResourceNode("BP_ResourceNode35", "resourceNode", "Desc_OreIron_C", "normal")], nodes);
    }

    [Fact]
    public async Task An_extractor_is_found_by_its_class_name_with_what_it_extracts_per_cycle()
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<Contracts.ICatalogQueries>();

        IReadOnlyList<Contracts.CatalogExtractor> extractors = await catalog.GetExtractorsAsync(
            ["Build_MinerMk1_C", "Build_MinerMk9_C"], TestContext.Current.CancellationToken);

        Assert.Equal([new Contracts.CatalogExtractor("Build_MinerMk1_C", 1, 1)], extractors);
    }

    [Fact]
    public async Task A_footprint_is_found_by_the_class_name_of_its_building()
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<Contracts.ICatalogQueries>();

        IReadOnlyList<Contracts.CatalogFootprint> footprints = await catalog.GetFootprintsAsync(
            ["Build_ConstructorMk1_C", "Build_ConveyorLiftMk1_C"], TestContext.Current.CancellationToken);

        Contracts.CatalogFootprint constructor = Assert.Single(footprints);
        Assert.Equal(("Build_ConstructorMk1_C", -400m, -500m, 400m, 500m), (constructor.Id, constructor.MinX, constructor.MinY, constructor.MaxX, constructor.MaxY));
        Assert.NotEmpty(constructor.Volumes);
    }

    [Fact]
    public async Task A_generator_is_found_by_its_class_name_with_its_power_and_fuels()
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<Contracts.ICatalogQueries>();

        IReadOnlyList<Contracts.CatalogGenerator> generators = await catalog.GetGeneratorsAsync(
            ["Build_GeneratorNuclear_C", "Build_ResourceSink_C"], TestContext.Current.CancellationToken);

        Contracts.CatalogGenerator plant = Assert.Single(generators);
        Assert.Equal((2500m, 1.6m), (plant.PowerMegawatts, plant.SupplementalLitresPerMegajoule));
        Assert.Contains(new Contracts.CatalogGeneratorFuel("Desc_NuclearFuelRod_C", 750_000, "Desc_Water_C", "Desc_NuclearWaste_C", 50), plant.Fuels);
    }

    [Fact]
    public async Task A_drone_port_is_found_by_its_class_name_with_its_trip_cost_and_fuels()
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<Contracts.ICatalogQueries>();

        IReadOnlyList<Contracts.CatalogDronePort> ports = await catalog.GetDronePortsAsync(
            ["Build_DroneStation_C", "Build_TruckStation_C"], TestContext.Current.CancellationToken);

        Contracts.CatalogDronePort port = Assert.Single(ports);
        Assert.Equal((24_000m, 6m), (port.TripEnergyMegajoules, port.TripEnergyPerMetreMegajoules));
        Assert.Contains(new Contracts.CatalogDroneFuel("Desc_PackagedRocketFuel_C", 7_200), port.Fuels);
    }

    [Fact]
    public async Task Transport_tiers_are_found_by_class_name_with_their_capacity_per_minute()
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<Contracts.ICatalogQueries>();

        IReadOnlyList<Contracts.CatalogTransportTier> tiers = await catalog.GetTransportTiersAsync(
            ["Build_ConveyorBeltMk1_C", "Build_ConveyorLiftMk1_C", "Build_PipelineMK2_NoIndicator_C", "Build_PipelinePump_C"],
            TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new Contracts.CatalogTransportTier("Build_ConveyorBeltMk1_C", 60),
                new Contracts.CatalogTransportTier("Build_ConveyorLiftMk1_C", 60),
                new Contracts.CatalogTransportTier("Build_PipelineMK2_NoIndicator_C", 600),
            ],
            tiers);
    }
}
