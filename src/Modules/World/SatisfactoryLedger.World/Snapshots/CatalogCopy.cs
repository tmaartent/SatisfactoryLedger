using SatisfactoryLedger.Catalog.Contracts;

namespace SatisfactoryLedger.World.Snapshots;

/// <summary>What the Catalog said, at import, about the recipes, extractors, belts, nodes, items and footprints a save uses.</summary>
internal sealed record CatalogCopy(
    IReadOnlyCollection<CatalogRecipe> Recipes,
    IReadOnlyCollection<CatalogExtractor> Extractors,
    IReadOnlyCollection<CatalogTransportTier> TransportTiers,
    IReadOnlyCollection<CatalogResourceNode> ResourceNodes,
    IReadOnlyCollection<CatalogItem> Items,
    IReadOnlyCollection<CatalogFootprint> Footprints,
    IReadOnlyCollection<CatalogGenerator> Generators,
    IReadOnlyCollection<CatalogDronePort>? DronePorts = null,
    IReadOnlyCollection<CatalogStorage>? Storages = null);