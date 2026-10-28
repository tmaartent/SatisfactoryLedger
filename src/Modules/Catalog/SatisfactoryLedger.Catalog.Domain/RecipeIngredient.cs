namespace SatisfactoryLedger.Catalog.Domain;

public record RecipeIngredient(ItemId ItemId, int Amount)
{
    public static RecipeIngredient Of(ItemId itemId, int amount)
    {
        return new RecipeIngredient(itemId, amount);
    }
}