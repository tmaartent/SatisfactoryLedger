namespace SatisfactoryLedger.Mcp.LedgerApi;

/// <summary>An item a recipe takes or makes, with its rate.</summary>
internal sealed record IngredientResponse(string ItemId, RateResponse Rate);
