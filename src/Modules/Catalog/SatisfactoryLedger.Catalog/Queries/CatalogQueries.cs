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

    public async Task<IReadOnlyList<CatalogFootprint>> GetFootprintsAsync(IReadOnlyCollection<string> buildingIds, CancellationToken cancellationToken)
    {
        List<FootprintId> ids = [.. buildingIds.Select(FootprintId.Of)];
        List<Footprint> footprints = await db.Footprints.AsNoTracking()
            .Where(footprint => ids.Contains(footprint.Id))
            .ToListAsync(cancellationToken);

        return
        [
            .. footprints.OrderBy(footprint => footprint.Id.Id, StringComparer.Ordinal)
                .Select(footprint => new CatalogFootprint(footprint.Id.Id, footprint.MinX, footprint.MinY, footprint.MaxX, footprint.MaxY,
                    [.. footprint.Volumes.Select(v => new CatalogVolume(
                        v.MinX, v.MinY, v.MinZ, v.MaxX, v.MaxY, v.MaxZ, v.RotationX, v.RotationY, v.RotationZ, v.RotationW, v.OffsetX, v.OffsetY, v.OffsetZ))]))
        ];
    }

    public async Task<IReadOnlyList<CatalogGenerator>> GetGeneratorsAsync(IReadOnlyCollection<string> generatorIds, CancellationToken cancellationToken)
    {
        List<GeneratorId> ids = [.. generatorIds.Select(GeneratorId.Of)];
        List<Generator> generators = await db.Generators.AsNoTracking()
            .Where(generator => ids.Contains(generator.Id))
            .ToListAsync(cancellationToken);

        return
        [
            .. generators.OrderBy(generator => generator.Id.Id, StringComparer.Ordinal)
                .Select(generator => new CatalogGenerator(
                    generator.Id.Id,
                    generator.PowerMegawatts,
                    generator.SupplementalLitresPerMegajoule,
                    [
                        .. generator.Fuels.Select(fuel => new CatalogGeneratorFuel(
                            fuel.Fuel.Id, fuel.EnergyMegajoules, fuel.Supplemental?.Id, fuel.Byproduct?.Id, fuel.ByproductPerFuel))
                    ]))
        ];
    }

    public async Task<IReadOnlyList<CatalogDronePort>> GetDronePortsAsync(IReadOnlyCollection<string> dronePortIds, CancellationToken cancellationToken)
    {
        List<DronePortId> ids = [.. dronePortIds.Select(DronePortId.Of)];
        List<DronePort> ports = await db.DronePorts.AsNoTracking()
            .Where(port => ids.Contains(port.Id))
            .ToListAsync(cancellationToken);

        return
        [
            .. ports.OrderBy(port => port.Id.Id, StringComparer.Ordinal)
                .Select(port => new CatalogDronePort(
                    port.Id.Id,
                    port.TripEnergyMegajoules,
                    port.TripEnergyPerMetreMegajoules,
                    [.. port.Fuels.Select(fuel => new CatalogDroneFuel(fuel.Fuel.Id, fuel.EnergyMegajoules))]))
        ];
    }
}
