namespace SatisfactoryLedger.Catalog.Contracts;

/// <summary>
/// What other modules may ask the Catalog (ADR-0003). Ids are the game's class names, so a module that reads them
/// from a save can look them up as they are.
/// </summary>
public interface ICatalogQueries
{
    /// <summary>The recipes with these ids that the Catalog knows, including ones no longer in the game. Unknown ids are left out.</summary>
    Task<IReadOnlyList<CatalogRecipe>> GetRecipesAsync(IReadOnlyCollection<string> recipeIds, CancellationToken cancellationToken);

    /// <summary>The items with these ids that the Catalog knows, including ones no longer in the game. Unknown ids are left out.</summary>
    Task<IReadOnlyList<CatalogItem>> GetItemsAsync(IReadOnlyCollection<string> itemIds, CancellationToken cancellationToken);

    /// <summary>The resource nodes with these names that the Catalog knows (ADR-0015). Unknown names, such as water volumes, are left out.</summary>
    Task<IReadOnlyList<CatalogResourceNode>> GetResourceNodesAsync(IReadOnlyCollection<string> names, CancellationToken cancellationToken);

    /// <summary>The extractors with these building class names that the Catalog knows. Unknown ids are left out.</summary>
    Task<IReadOnlyList<CatalogExtractor>> GetExtractorsAsync(IReadOnlyCollection<string> extractorIds, CancellationToken cancellationToken);

    /// <summary>The belts, lifts and pipes with these building class names that the Catalog knows. Unknown ids are left out.</summary>
    Task<IReadOnlyList<CatalogTransportTier>> GetTransportTiersAsync(IReadOnlyCollection<string> tierIds, CancellationToken cancellationToken);

    /// <summary>The footprints of the buildings with these class names that the Catalog knows (ADR-0020). Unknown ids, such as lifts, are left out.</summary>
    Task<IReadOnlyList<CatalogFootprint>> GetFootprintsAsync(IReadOnlyCollection<string> buildingIds, CancellationToken cancellationToken);

    /// <summary>The generators with these class names that the Catalog knows (BR-A8). Unknown ids are left out.</summary>
    Task<IReadOnlyList<CatalogGenerator>> GetGeneratorsAsync(IReadOnlyCollection<string> generatorIds, CancellationToken cancellationToken);

    /// <summary>The drone ports with these class names that the Catalog knows (BR-A16). Unknown ids are left out.</summary>
    Task<IReadOnlyList<CatalogDronePort>> GetDronePortsAsync(IReadOnlyCollection<string> dronePortIds, CancellationToken cancellationToken);
}