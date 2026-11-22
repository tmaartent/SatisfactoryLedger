namespace SatisfactoryLedger.Mcp.Answers;

/// <summary>A recipe: the building it runs in, and what it takes and makes per minute at 100% clock speed.</summary>
internal sealed record RecipeAnswer(
    string Id, string Name, string Building, bool Alternate, IReadOnlyList<IngredientAnswer> Inputs, IReadOnlyList<IngredientAnswer> Outputs);
