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

    public async Task<IReadOnlyList<CatalogItem>> GetItemsAsync(IReadOnlyCollection<string> itemIds, CancellationToken cancellationToken)
    {
        List<ItemId> ids = [.. itemIds.Select(ItemId.Of)];
        return await db.Items.AsNoTracking()
            .Where(item => ids.Contains(item.Id))
            .OrderBy(item => item.Name)
            .Select(item => new CatalogItem(item.Id.Id, item.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogResourceNode>> GetResourceNodesAsync(IReadOnlyCollection<string> names, CancellationToken cancellationToken)
    {
        List<ResourceNodeId> ids = [.. names.Select(ResourceNodeId.Of)];
        List<ResourceNode> nodes = await db.ResourceNodes.AsNoTracking()
            .Where(node => ids.Contains(node.Id))
            .ToListAsync(cancellationToken);

        // The contract has no enums of the Catalog's own: kinds and purities go out as the API writes them.
        return
        [
            .. nodes.OrderBy(node => node.Id.Id, StringComparer.Ordinal).Select(node => new CatalogResourceNode(
                node.Id.Id,
                node.Kind == ResourceNodeKind.WellSatellite ? "wellSatellite" : "resourceNode",
                node.Resource.Id,
                node.Purity.ToString().ToLowerInvariant()))
        ];
    }
}
