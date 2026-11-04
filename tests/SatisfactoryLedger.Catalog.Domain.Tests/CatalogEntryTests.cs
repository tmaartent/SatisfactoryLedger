namespace SatisfactoryLedger.Catalog.Domain.Tests;

public class CatalogEntryTests
{
    [Fact]
    public void A_new_item_is_in_the_game()
    {
        Assert.True(TestCatalog.Items.IronPlate.IsInGame);
    }

    [Fact]
    public void An_item_marked_no_longer_in_game_is_not_in_the_game()
    {
        var ironPlate = TestCatalog.Items.IronPlate;

        ironPlate.MarkNoLongerInGame();

        Assert.False(ironPlate.IsInGame);
    }

    [Fact]
    public void Updating_an_item_from_the_game_takes_the_game_data()
    {
        var stored = Item.Create(ItemId.Of("Desc_Water_C"), "Water", ItemForm.Solid, false);

        stored.UpdateFromGame(Item.Create(ItemId.Of("Desc_Water_C"), "Fresh Water", ItemForm.Fluid, true));

        Assert.Equal("Fresh Water", stored.Name);
        Assert.Equal(ItemForm.Fluid, stored.ItemForm);
        Assert.True(stored.IsRawResource);
    }

    [Fact]
    public void An_item_that_is_back_in_the_game_is_in_the_game_again()
    {
        var ironPlate = TestCatalog.Items.IronPlate;
        ironPlate.MarkNoLongerInGame();

        ironPlate.UpdateFromGame(TestCatalog.Items.IronPlate);

        Assert.True(ironPlate.IsInGame);
    }

    [Fact]
    public void An_item_cannot_be_updated_from_a_different_item()
    {
        Assert.Throws<ArgumentException>(() => TestCatalog.Items.IronPlate.UpdateFromGame(TestCatalog.Items.IronIngot));
    }

    [Fact]
    public void Updating_a_building_from_the_game_takes_the_game_data()
    {
        var stored = Building.Create(BuildingId.Of("Build_ConstructorMk1_C"), "Constructor", 0);
        stored.MarkNoLongerInGame();

        stored.UpdateFromGame(Building.Create(BuildingId.Of("Build_ConstructorMk1_C"), "Constructor Mk.1", 1));

        Assert.Equal("Constructor Mk.1", stored.Name);
        Assert.Equal(1, stored.SomersloopSlots);
        Assert.True(stored.IsInGame);
    }

    [Fact]
    public void Updating_a_transport_tier_from_the_game_takes_the_game_data()
    {
        var stored = TransportTier.Create(TransportTierId.Of("Build_ConveyorBeltMk1_C"), "Belt", 60, TransportForm.Belt);
        stored.MarkNoLongerInGame();

        stored.UpdateFromGame(TransportTier.Create(TransportTierId.Of("Build_ConveyorBeltMk1_C"), "Conveyor Belt Mk.1", 120, TransportForm.Belt));

        Assert.Equal("Conveyor Belt Mk.1", stored.Name);
        Assert.Equal(120, stored.Capacity);
        Assert.True(stored.IsInGame);
    }

    [Fact]
    public void Updating_a_recipe_from_the_game_takes_the_game_data()
    {
        var stored = IronPlateRecipe(cycleSeconds: 6, platesOut: 2);
        stored.MarkNoLongerInGame();

        stored.UpdateFromGame(IronPlateRecipe(cycleSeconds: 4, platesOut: 3));

        Assert.Equal(4, stored.CycleSeconds);
        Assert.Equal([RecipeIngredient.Of(TestCatalog.Items.IronPlate.Id, 3)], stored.Outputs);
        Assert.True(stored.IsInGame);
    }

    private static Recipe IronPlateRecipe(decimal cycleSeconds, decimal platesOut) =>
        Recipe.Create(
            RecipeId.Of("Recipe_IronPlate_C"),
            "Iron Plate",
            BuildingId.Of("Build_ConstructorMk1_C"),
            cycleSeconds,
            [RecipeIngredient.Of(TestCatalog.Items.IronIngot.Id, 3)],
            [RecipeIngredient.Of(TestCatalog.Items.IronPlate.Id, platesOut)],
            false);
}