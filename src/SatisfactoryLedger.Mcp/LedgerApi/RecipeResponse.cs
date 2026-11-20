namespace SatisfactoryLedger.Mcp.LedgerApi;

/// <summary>A recipe of the Catalog, with what it takes and makes per minute at 100% clock speed.</summary>
internal sealed record RecipeResponse(
    string Id, string Name, string BuildingId, bool IsAlternate, IReadOnlyList<IngredientResponse> Inputs, IReadOnlyList<IngredientResponse> Outputs);
