using Microsoft.EntityFrameworkCore;

using SatisfactoryLedger.Catalog.Contracts;
using SatisfactoryLedger.Catalog.Domain;
using SatisfactoryLedger.Catalog.Persistence;

namespace SatisfactoryLedger.Catalog.Queries;

/// <summary>Answers what other modules ask the Catalog, in the shape of <c>Catalog.Contracts</c> (Open Host Service).</summary>
internal sealed class CatalogQueries(CatalogDbContext db) : ICatalogQueries
{
    public async Task<IReadOnlyList<CatalogRecipe>> GetRecipesAsync(IReadOnlyCollection<string> recipeIds, CancellationToken cancellationToken)
    {
        List<RecipeId> ids = [.. recipeIds.Select(RecipeId.Of)];
        List<Recipe> recipes = await db.Recipes.AsNoTracking()
            .Where(recipe => ids.Contains(recipe.Id))
            .ToListAsync(cancellationToken);

        return
        [
            .. recipes.Select(recipe => new CatalogRecipe(
                recipe.Id.Id,
                recipe.BuildingId.Id,
                recipe.CycleSeconds,
                [.. recipe.Inputs.Select(input => new CatalogIngredient(input.ItemId.Id, input.Amount))],
                [.. recipe.Outputs.Select(output => new CatalogIngredient(output.ItemId.Id, output.Amount))]))
        ];
    }
}
