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
            .Select(item => new CatalogItem(item.Id.Id, item.Name, item.ItemForm == ItemForm.Fluid))
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

    public async Task<IReadOnlyList<CatalogExtractor>> GetExtractorsAsync(IReadOnlyCollection<string> extractorIds, CancellationToken cancellationToken)
    {
        List<ExtractorId> ids = [.. extractorIds.Select(ExtractorId.Of)];
        List<Extractor> extractors = await db.Extractors.AsNoTracking()
            .Where(extractor => ids.Contains(extractor.Id))
            .ToListAsync(cancellationToken);

        return
        [
            .. extractors.OrderBy(extractor => extractor.Id.Id, StringComparer.Ordinal)
                .Select(extractor => new CatalogExtractor(extractor.Id.Id, extractor.AmountPerCycle, extractor.CycleSeconds))
        ];
    }

    public async Task<IReadOnlyList<CatalogTransportTier>> GetTransportTiersAsync(IReadOnlyCollection<string> tierIds, CancellationToken cancellationToken)
    {
        List<TransportTierId> ids = [.. tierIds.Select(TransportTierId.Of)];
        List<TransportTier> tiers = await db.TransportTiers.AsNoTracking()
            .Where(tier => ids.Contains(tier.Id))
            .ToListAsync(cancellationToken);

        return
        [
            .. tiers.OrderBy(tier => tier.Id.Id, StringComparer.Ordinal)
                .Select(tier => new CatalogTransportTier(tier.Id.Id, tier.Capacity))
        ];
    }
}
