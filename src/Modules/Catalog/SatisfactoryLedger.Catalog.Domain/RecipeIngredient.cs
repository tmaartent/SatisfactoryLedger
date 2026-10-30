namespace SatisfactoryLedger.Catalog.Domain;

public record RecipeIngredient(ItemId ItemId, decimal Amount)
{
    public static RecipeIngredient Of(ItemId itemId, decimal amount)
    {
        return new RecipeIngredient(itemId, amount);
    }
}