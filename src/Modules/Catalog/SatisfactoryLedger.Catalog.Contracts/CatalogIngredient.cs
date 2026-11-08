namespace SatisfactoryLedger.Catalog.Contracts;

/// <param name="ItemId">The item's class name, e.g. <c>Desc_IronPlate_C</c>.</param>
/// <param name="AmountPerCycle">Items per cycle, or cubic metres for a fluid.</param>
public sealed record CatalogIngredient(string ItemId, decimal AmountPerCycle);