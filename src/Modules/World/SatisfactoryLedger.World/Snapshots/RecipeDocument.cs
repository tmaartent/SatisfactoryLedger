using SatisfactoryLedger.Catalog.Contracts;
using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.Snapshots;

internal sealed record RecipeDocument(string Id, decimal CycleSeconds, IReadOnlyList<IngredientDocument> Inputs, IReadOnlyList<IngredientDocument> Outputs)
{
    public static RecipeDocument From(CatalogRecipe recipe) => new(
        recipe.Id,
        recipe.CycleSeconds,
        [.. recipe.Inputs.Select(input => new IngredientDocument(input.ItemId, input.AmountPerCycle))],
        [.. recipe.Outputs.Select(output => new IngredientDocument(output.ItemId, output.AmountPerCycle))]);

    /// <summary>Per minute at 100%. An item that appears twice on one side counts once, with both amounts.</summary>
    public Recipe ToRecipe() => new(PerMinute(Inputs), PerMinute(Outputs));

    private Dictionary<ItemId, Rate> PerMinute(IEnumerable<IngredientDocument> ingredients) => ingredients
        .GroupBy(ingredient => ingredient.Item, StringComparer.Ordinal)
        .ToDictionary(group => ItemId.Of(group.Key), group => Rate.FromRecipe(group.Sum(ingredient => ingredient.AmountPerCycle), CycleSeconds));
}