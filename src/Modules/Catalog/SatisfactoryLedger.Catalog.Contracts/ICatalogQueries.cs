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
}

/// <param name="Id">The item's class name, e.g. <c>Desc_OreIron_C</c>.</param>
/// <param name="Name">The name the game shows, e.g. "Iron Ore".</param>
public sealed record CatalogItem(string Id, string Name);

/// <param name="Id">The recipe's class name, e.g. <c>Recipe_IronPlate_C</c>.</param>
/// <param name="BuildingId">The building that runs it, e.g. <c>Build_ConstructorMk1_C</c>.</param>
public sealed record CatalogRecipe(
    string Id,
    string BuildingId,
    decimal CycleSeconds,
    IReadOnlyList<CatalogIngredient> Inputs,
    IReadOnlyList<CatalogIngredient> Outputs);

/// <param name="ItemId">The item's class name, e.g. <c>Desc_IronPlate_C</c>.</param>
/// <param name="AmountPerCycle">Items per cycle, or cubic metres for a fluid.</param>
public sealed record CatalogIngredient(string ItemId, decimal AmountPerCycle);
