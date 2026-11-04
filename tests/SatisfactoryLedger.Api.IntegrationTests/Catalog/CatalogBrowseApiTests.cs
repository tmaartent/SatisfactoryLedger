namespace SatisfactoryLedger.Api.IntegrationTests.Catalog;

public sealed class CatalogBrowseApiTests(LedgerApiFactory api) : IAsyncLifetime
{
    private readonly HttpClient _client = api.CreateClient();

    public async ValueTask InitializeAsync()
    {
        await api.ResetCatalogAsync();
        await _client.ImportAsync(CatalogClient.GameFile);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Items_can_be_limited_to_fluids()
    {
        var fluids = await _client.GetAsync<CatalogItem[]>("/api/v1/catalog/items?form=fluid");

        Assert.Equal(15, fluids.Length);
        Assert.All(fluids, item => Assert.Equal("fluid", item.Form));
        Assert.Contains(fluids, item => item is { Id: "Desc_Water_C", IsRawResource: true });
    }

    [Fact]
    public async Task An_unknown_item_form_is_rejected()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/catalog/items?form=plasma", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Recipes_for_an_item_include_its_alternates()
    {
        var recipes = await _client.GetAsync<CatalogRecipe[]>("/api/v1/catalog/recipes?produces=Desc_IronPlate_C");

        string[] ids = [.. recipes.Select(recipe => recipe.Id)];
        Assert.Contains("Recipe_IronPlate_C", ids);
        Assert.Contains("Recipe_Alternate_CoatedIronPlate_C", ids);
        Assert.Contains("Recipe_Alternate_SteelCastedPlate_C", ids);
        Assert.All(recipes, recipe => Assert.Contains(recipe.Outputs, output => output.ItemId == "Desc_IronPlate_C"));
    }

    [Fact]
    public async Task A_recipe_shows_its_rates_per_minute_both_as_decimal_and_exact()
    {
        var recipes = await _client.GetAsync<CatalogRecipe[]>("/api/v1/catalog/recipes?produces=Desc_IronPlate_C");

        CatalogRecipe ironPlate = Assert.Single(recipes, recipe => recipe.Id == "Recipe_IronPlate_C");
        Assert.Equal(new CatalogIngredient("Desc_IronIngot_C", 3, new CatalogRate(30, "30/1")), Assert.Single(ironPlate.Inputs));
        Assert.Equal(new CatalogIngredient("Desc_IronPlate_C", 2, new CatalogRate(20, "20/1")), Assert.Single(ironPlate.Outputs));
    }

    [Fact]
    public async Task A_recipe_with_a_non_round_rate_keeps_the_exact_fraction()
    {
        var recipes = await _client.GetAsync<CatalogRecipe[]>("/api/v1/catalog/recipes?produces=Desc_RocketFuel_C");

        // 2.4 seconds per cycle, so 60 / 2.4 = 25 cycles per minute.
        CatalogRecipe nitroRocketFuel = Assert.Single(recipes, recipe => recipe.Id == "Recipe_Alternate_RocketFuel_Nitro_C");
        Assert.Equal(2.4m, nitroRocketFuel.CycleSeconds);
        Assert.All(nitroRocketFuel.Inputs.Concat(nitroRocketFuel.Outputs),
            ingredient => Assert.Equal(ingredient.AmountPerCycle * 25, ingredient.Rate.PerMinute));
    }

    [Fact]
    public async Task Transport_tiers_are_belts_and_lifts_then_pipes_from_low_to_high_capacity_then_by_name()
    {
        var tiers = await _client.GetAsync<CatalogTransportTier[]>("/api/v1/catalog/transport-tiers");

        Assert.Equal(
            [
                "Conveyor Belt Mk.1:60", "Conveyor Lift Mk.1:60", "Conveyor Belt Mk.2:120", "Conveyor Lift Mk.2:120",
                "Conveyor Belt Mk.3:270", "Conveyor Lift Mk.3:270", "Conveyor Belt Mk.4:480", "Conveyor Lift Mk.4:480",
                "Conveyor Belt Mk.5:780", "Conveyor Lift Mk.5:780", "Conveyor Belt Mk.6:1200", "Conveyor Lift Mk.6:1200",
                "Clean Pipeline Mk.1:300", "Pipeline Mk.1:300", "Clean Pipeline Mk.2:600", "Pipeline Mk.2:600"
            ],
            tiers.Select(tier => $"{tier.Name}:{tier.Capacity}"));
    }

    [Fact]
    public async Task Extractors_are_listed_with_what_they_extract_on_a_normal_node()
    {
        var extractors = await _client.GetAsync<CatalogExtractor[]>("/api/v1/catalog/extractors");

        Assert.Equal(
            ["Miner Mk.1", "Miner Mk.2", "Miner Mk.3", "Oil Extractor", "Resource Well Extractor", "Water Extractor"],
            extractors.Select(extractor => extractor.Name));

        CatalogExtractor minerMk3 = Assert.Single(extractors, extractor => extractor.Id == "Build_MinerMk3_C");
        Assert.Equal("solid", minerMk3.Form);
        Assert.Equal(new CatalogRate(240, "240/1"), minerMk3.ExtractionRate);

        CatalogExtractor water = Assert.Single(extractors, extractor => extractor.Id == "Build_WaterPump_C");
        Assert.Equal("fluid", water.Form);
        Assert.Equal(2, water.AmountPerCycle);
        Assert.Equal(new CatalogRate(120, "120/1"), water.ExtractionRate);
    }
}
