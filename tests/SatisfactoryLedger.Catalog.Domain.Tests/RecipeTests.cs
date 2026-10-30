using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.Catalog.Domain.Tests;

public class RecipeTests
{
    [Fact]
    public void Iron_plate_recipe_consumes_30_ingots_and_produces_20_plates_per_minute()
    {
        var ironPlates20 = Recipe.Create(
            RecipeId.Of("IronPlates20"),
            "Iron_plate_recipe_consumes_30_ingots_and_produces_20_plates_per_minute",
            BuildingId.Of("Constructor1"),
            6,
            [
                RecipeIngredient.Of(TestCatalog.Items.IronIngot.Id, 3)
            ],
            [
                RecipeIngredient.Of(TestCatalog.Items.IronPlate.Id, 2)
            ],
            false
        );


        var inputRates = Assert.Single(ironPlates20.BaseInputRates());
        var outputRates = Assert.Single(ironPlates20.BaseOutputRates());

        Assert.Equal(ItemRate.Of(TestCatalog.Items.IronIngot.Id, Rate.PerMinute(30)), inputRates);
        Assert.Equal(ItemRate.Of(TestCatalog.Items.IronPlate.Id, Rate.PerMinute(20)), outputRates);
    }

    [Fact]
    public void Recipes_with_different_ids_are_not_equal()
    {
        var recipe1 = Recipe.Create(
            RecipeId.Of("IronPlates20"),
            "Foo",
            BuildingId.Of("Constructor1"),
            6,
            [
                RecipeIngredient.Of(TestCatalog.Items.IronIngot.Id, 3)
            ],
            [
                RecipeIngredient.Of(TestCatalog.Items.IronPlate.Id, 2)
            ],
            false
        );
        
        var recipe2 = Recipe.Create(
            RecipeId.Of("IronPlates21"),
            "Bar",
            BuildingId.Of("Constructor1"),
            6,
            [
                RecipeIngredient.Of(TestCatalog.Items.IronIngot.Id, 3)
            ],
            [
                RecipeIngredient.Of(TestCatalog.Items.IronPlate.Id, 2)
            ],
            false
        );
        
        Assert.NotEqual(recipe1, recipe2);
        
        Assert.NotEqual(recipe1.GetHashCode(), recipe2.GetHashCode());
    }
    
    [Fact]
    public void Recipes_with_same_ids_are_equal()
    {
        var recipe1 = Recipe.Create(
            RecipeId.Of("IronPlates20"),
            "Foo",
            BuildingId.Of("Constructor1"),
            6,
            [
                RecipeIngredient.Of(TestCatalog.Items.IronIngot.Id, 3)
            ],
            [
                RecipeIngredient.Of(TestCatalog.Items.IronPlate.Id, 2)
            ],
            false
        );
        
        var recipe2 = Recipe.Create(
            RecipeId.Of("IronPlates20"),
            "Bar",
            BuildingId.Of("Constructor1"),
            6,
            [
                RecipeIngredient.Of(TestCatalog.Items.IronIngot.Id, 3)
            ],
            [
                RecipeIngredient.Of(TestCatalog.Items.IronPlate.Id, 2)
            ],
            false
        );
        
        Assert.Equal(recipe1, recipe2);
        
        Assert.Equal(recipe1.GetHashCode(), recipe2.GetHashCode());
    }

    [Fact]
    public void Throws_if_cycleSeconds_less_than_1()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Recipe.Create(
            RecipeId.Of("IronPlates20"),
            "Bar",
            BuildingId.Of("Constructor1"),
            -1,
            [
                RecipeIngredient.Of(TestCatalog.Items.IronIngot.Id, 3)
            ],
            [
                RecipeIngredient.Of(TestCatalog.Items.IronPlate.Id, 2)
            ],
            false
        ));
    }
    
    [Fact]
    public void Throws_if_cycleSeconds_is_zero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Recipe.Create(
            RecipeId.Of("IronPlates20"),
            "Bar",
            BuildingId.Of("Constructor1"),
            0,
            [
                RecipeIngredient.Of(TestCatalog.Items.IronIngot.Id, 3)
            ],
            [
                RecipeIngredient.Of(TestCatalog.Items.IronPlate.Id, 2)
            ],
            false
        ));
    }
    
    [Fact]
    public void Throws_if_name_is_empty()
    {
        Assert.Throws<ArgumentException>(() => Recipe.Create(
            RecipeId.Of("IronPlates20"),
            "",
            BuildingId.Of("Constructor1"),
            1,
            [
                RecipeIngredient.Of(TestCatalog.Items.IronIngot.Id, 3)
            ],
            [
                RecipeIngredient.Of(TestCatalog.Items.IronPlate.Id, 2)
            ],
            false
        ));
    }
    
    
    [Fact]
    public void Throws_if_inputs_more_than_four()
    {
        Assert.Throws<ArrayArgumentException>(() => Recipe.Create(
            RecipeId.Of("IronPlates20"),
            "Foo",
            BuildingId.Of("Constructor1"),
            1,
            [
                RecipeIngredient.Of(TestCatalog.Items.IronIngot.Id, 3),
                RecipeIngredient.Of(TestCatalog.Items.IronOre.Id, 3),
                RecipeIngredient.Of(TestCatalog.Items.Wire.Id, 3),
                RecipeIngredient.Of(TestCatalog.Items.ReinforcedIronPlate.Id, 3),
                RecipeIngredient.Of(TestCatalog.Items.IronPlate.Id, 3)
            ],
            [
                RecipeIngredient.Of(TestCatalog.Items.IronPlate.Id, 2)
            ],
            false
        ));
    }
    
    [Fact]
    public void Throws_if_outputs_less_than_one()
    {
        Assert.Throws<ArrayArgumentException>(() => Recipe.Create(
            RecipeId.Of("IronPlates20"),
            "Foo",
            BuildingId.Of("Constructor1"),
            1,
            [
                RecipeIngredient.Of(TestCatalog.Items.IronIngot.Id, 3)
            ],
            [
            ],
            false
        ));
    }
    
    [Fact]
    public void Throws_if_outputs_more_than_two()
    {
        Assert.Throws<ArrayArgumentException>(() => Recipe.Create(
            RecipeId.Of("IronPlates20"),
            "Foo",
            BuildingId.Of("Constructor1"),
            1,
            [
                RecipeIngredient.Of(TestCatalog.Items.IronIngot.Id, 3),
            ],
            [
                RecipeIngredient.Of(TestCatalog.Items.IronPlate.Id, 2),
                RecipeIngredient.Of(TestCatalog.Items.Wire.Id, 2),
                RecipeIngredient.Of(TestCatalog.Items.ReinforcedIronPlate.Id, 2)
            ],
            false
        ));
    }
}