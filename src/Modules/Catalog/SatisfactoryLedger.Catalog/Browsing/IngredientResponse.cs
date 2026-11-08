namespace SatisfactoryLedger.Catalog.Browsing;

internal sealed record IngredientResponse(string ItemId, decimal AmountPerCycle, RateResponse Rate);