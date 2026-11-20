namespace SatisfactoryLedger.Mcp.Answers;

/// <summary>An item a recipe takes or makes, per minute at 100% clock speed.</summary>
internal sealed record IngredientAnswer(string Item, double PerMinute);
