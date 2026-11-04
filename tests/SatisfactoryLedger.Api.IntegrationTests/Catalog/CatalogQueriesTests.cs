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
}
