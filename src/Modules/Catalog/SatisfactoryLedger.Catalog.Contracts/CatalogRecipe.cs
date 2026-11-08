namespace SatisfactoryLedger.Catalog.Contracts;

/// <param name="Id">The recipe's class name, e.g. <c>Recipe_IronPlate_C</c>.</param>
/// <param name="BuildingId">The building that runs it, e.g. <c>Build_ConstructorMk1_C</c>.</param>
public sealed record CatalogRecipe(
    string Id,
    string BuildingId,
    decimal CycleSeconds,
    IReadOnlyList<CatalogIngredient> Inputs,
    IReadOnlyList<CatalogIngredient> Outputs);