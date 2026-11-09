namespace SatisfactoryLedger.Catalog.Browsing.Dto.Responses;

internal sealed record IngredientResponse(string ItemId, decimal AmountPerCycle, RateResponse Rate);