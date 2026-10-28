namespace SatisfactoryLedger.Catalog.Domain.Tests;

public class RecipeIngredientTests
{
    [Fact]
    public void RecipeIngredient_can_be_created()
    {
        RecipeIngredient recipeIngredient = RecipeIngredient.Of(TestCatalog.Items.IronPlate.Id, 12);
        
        Assert.Equal("Desc_IronPlate_C", recipeIngredient.ItemId.Id);
        Assert.Equal(12m, recipeIngredient.Amount);
    }
}