using SatisfactoryLedger.Catalog.Domain.GameObjects;

namespace SatisfactoryLedger.Catalog.Browsing.Dto.Responses;

internal sealed record RecipeResponse(
    string Id,
    string Name,
    string BuildingId,
    decimal CycleSeconds,
    bool IsAlternate,
    bool InGame,
    IReadOnlyList<IngredientResponse> Inputs,
    IReadOnlyList<IngredientResponse> Outputs)
{
    public static RecipeResponse From(Recipe recipe) => new(
        recipe.Id.Id,
        recipe.Name,
        recipe.BuildingId.Id,
        recipe.CycleSeconds,
        recipe.IsAlternate,
        recipe.IsInGame,
        ToResponses(recipe.Inputs, recipe.BaseInputRates()),
        ToResponses(recipe.Outputs, recipe.BaseOutputRates()));

    private static IngredientResponse[] ToResponses(IReadOnlyList<RecipeIngredient> ingredients, ItemRate[] rates) =>
    [
        .. ingredients.Zip(rates, (ingredient, rate) =>
            new IngredientResponse(ingredient.ItemId.Id, ingredient.Amount, RateResponse.From(rate.Rate)))
    ];
}