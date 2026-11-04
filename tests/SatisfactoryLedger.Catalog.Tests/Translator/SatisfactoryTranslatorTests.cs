using System.Globalization;

using SatisfactoryLedger.Catalog.Domain;
using SatisfactoryLedger.Catalog.Importer;
using SatisfactoryLedger.Catalog.Translator;

namespace SatisfactoryLedger.Catalog.Tests.Translator;

public class SatisfactoryTranslatorTests
{
    [Fact]
    public async Task Items_can_be_translated()
    {
        var resources = await SatisfactoryImporter.Import("Fixtures/Valid.json");
        var itemGroupDtos = resources.OfType<ItemGroupDto>().ToList();
        var resourceItemGroupDto = itemGroupDtos.Single(dto => dto is ResourceItemGroupDto);
        var totalCount = itemGroupDtos.Select(dto => dto.Items.Count).Sum();
        var itemDtos = itemGroupDtos.SelectMany(dto => dto.Items).ToDictionary(dto => dto.ClassName, dto => dto);
        
        List<Item> items = SatisfactoryTranslator.TranslateToItems(resources);
        int resourceCount = items.Count(item => item.IsRawResource);

        Assert.Equal(totalCount, items.Count);
        Assert.Equal(resourceItemGroupDto.Items.Count, resourceCount);
        
        items.ForEach(item =>
        {
            if (itemDtos.TryGetValue(item.Id.Id, out var itemDto))
            {
                Assert.Equal(itemDto.DisplayName, item.Name);
                switch (item.ItemForm)
                {
                    case ItemForm.Fluid:
                        Assert.True(itemDto.Form is "RF_LIQUID" or "RF_GAS");
                        break;
                    case ItemForm.Solid:
                        Assert.Equal("RF_SOLID", itemDto.Form);
                        break;
                    default:
                        Assert.Fail();
                        break;
                }
            }
            else
            {
                Assert.Fail();
            }
        });
    }

    [Fact]
    public async Task Buildings_can_be_translated()
    {
        var resources = await SatisfactoryImporter.Import("Fixtures/Valid.json");
        var buildingGroupDtos = resources.OfType<BuildingGroupDto>().ToList();
        var totalCount = buildingGroupDtos.Select(dto => dto.Buildings.Count).Sum();
        var buildingDtos = buildingGroupDtos.SelectMany(dto => dto.Buildings).ToDictionary(dto => dto.ClassName, dto => dto);
        
        List<Building> buildings = SatisfactoryTranslator.TranslateToBuildings(resources);
        
        Assert.Equal(totalCount, buildings.Count);
        
        buildings.ForEach(building =>
        {
            if (buildingDtos.TryGetValue(building.Id.Id, out var buildingDto))
            {
                Assert.Equal(buildingDto.DisplayName, building.Name);
                Assert.Equal(int.Parse(buildingDto.ProductionShardSlotSize, CultureInfo.InvariantCulture), building.SomersloopSlots);
            }
            else
            {
                Assert.Fail();
            }
        });
    }
    
    [Fact]
    public async Task TransportTiers_can_be_translated()
    {
        var resources = await SatisfactoryImporter.Import("Fixtures/Valid.json");
        var belTierGroupDtos = resources.OfType<BeltGroupDto>().ToList();
        var totalCount = belTierGroupDtos.Select(dto => dto.Tiers.Count).Sum();
        var pipeTierGroupDtos = resources.OfType<PipeGroupDto>().ToList();
        totalCount += pipeTierGroupDtos.Select(dto => dto.Tiers.Count).Sum();
        
        List<TransportTierDto> allTierDtos =
        [
            .. belTierGroupDtos.SelectMany(dto => dto.Tiers),
            .. pipeTierGroupDtos.SelectMany(dto => dto.Tiers)
        ];
        Dictionary<string, TransportTierDto> transportTierDtos = allTierDtos.ToDictionary(dto => dto.ClassName, dto => dto);
        
        List<TransportTier> transportTiers = SatisfactoryTranslator.TranslateToTransportTiers(resources);
        
        Assert.Equal(totalCount, transportTiers.Count);
        
        transportTiers.ForEach(transportTier =>
        {
            if (transportTierDtos.TryGetValue(transportTier.Id.Id, out var transportTierDto))
            {
                Assert.Equal(transportTierDto.DisplayName, transportTier.Name);
                if (transportTier.Form == TransportForm.Belt)
                {
                    Assert.True(transportTierDto is BeltDto);

                    if (transportTierDto is BeltDto belt)
                    {
                        Assert.Equal(decimal.Parse(belt.Speed, CultureInfo.InvariantCulture), transportTier.Capacity * 2);
                    }
                }
                else
                {
                    Assert.True(transportTierDto is PipeDto);
                    if (transportTierDto is PipeDto pipe)
                    {
                        Assert.Equal(decimal.Parse(pipe.FlowLimit, CultureInfo.InvariantCulture) * 60, transportTier.Capacity);
                    }
                }
            }
            else
            {
                Assert.Fail();
            }
        });
    }

    [Fact]
    public async Task Recipes_can_be_translated()
    {
        var resources = await SatisfactoryImporter.Import("Fixtures/Valid.json");
        var recipeGroupDtos = resources.OfType<RecipeGroupDto>().ToList();
        var totalCount = recipeGroupDtos.Select(dto => dto.Recipes.Count).Sum();
        var recipeDtos = recipeGroupDtos.SelectMany(dto => dto.Recipes).ToDictionary(dto => dto.ClassName, dto => dto);
        
        List<Recipe> recipes = SatisfactoryTranslator.TranslateToRecipes(resources);
        
        Assert.Equal(totalCount, recipes.Count);
        
        recipes.ForEach(recipe =>
        {
            if (recipeDtos.TryGetValue(recipe.Id.Id, out var recipeDto))
            {
                Assert.Equal(recipeDto.DisplayName, recipe.Name);
                Assert.Equal(recipeDto.RecipeBuilding!.ClassName, recipe.BuildingId.Id);
                Assert.Equal(decimal.Parse(recipeDto.ManufactoringDuration, CultureInfo.InvariantCulture), recipe.CycleSeconds);
                Assert.Equal(recipeDto.Ingredients.Count, recipe.Inputs.Count);
                Assert.Equal(recipeDto.Products.Count, recipe.Outputs.Count);
                Assert.Equal(recipeDto.ClassName.Contains("Alternate"), recipe.IsAlternate);
            }
            else
            {
                Assert.Fail();
            }
        });
    }

    // en-US.json is a snapshot of the game's Docs file. The expected values belong to that snapshot.
    [Fact]
    public async Task The_game_file_translates_into_a_complete_catalog()
    {
        var resources = await SatisfactoryImporter.Import("Fixtures/en-US.json");

        Assert.Equal(190, SatisfactoryTranslator.TranslateToItems(resources).Count);
        Assert.Equal(11, SatisfactoryTranslator.TranslateToBuildings(resources).Count);
        Assert.Equal(8, SatisfactoryTranslator.TranslateToTransportTiers(resources).Count);
        Assert.Equal(291, SatisfactoryTranslator.TranslateToRecipes(resources).Count);
    }

    [Fact]
    public async Task Water_from_the_game_file_is_a_raw_fluid()
    {
        var resources = await SatisfactoryImporter.Import("Fixtures/en-US.json");

        Item water = Assert.Single(SatisfactoryTranslator.TranslateToItems(resources), item => item.Id.Id == "Desc_Water_C");
        Assert.Equal(ItemForm.Fluid, water.ItemForm);
        Assert.True(water.IsRawResource);
    }

    [Fact]
    public async Task Transport_tiers_from_the_game_file_have_their_in_game_capacities()
    {
        var resources = await SatisfactoryImporter.Import("Fixtures/en-US.json");
        List<TransportTier> transportTiers = SatisfactoryTranslator.TranslateToTransportTiers(resources);

        int[] beltCapacities = [.. transportTiers.Where(tier => tier.Form == TransportForm.Belt).Select(tier => tier.Capacity).Order()];
        int[] pipeCapacities = [.. transportTiers.Where(tier => tier.Form == TransportForm.Pipe).Select(tier => tier.Capacity).Order()];

        Assert.Equal([60, 120, 270, 480, 780, 1200], beltCapacities);
        Assert.Equal([300, 600], pipeCapacities);
    }

    [Fact]
    public async Task The_iron_plate_recipe_from_the_game_file_turns_3_ingots_into_2_plates_in_6_seconds()
    {
        var resources = await SatisfactoryImporter.Import("Fixtures/en-US.json");

        Recipe ironPlate = Assert.Single(SatisfactoryTranslator.TranslateToRecipes(resources), recipe => recipe.Id.Id == "Recipe_IronPlate_C");
        Assert.Equal("Build_ConstructorMk1_C", ironPlate.BuildingId.Id);
        Assert.Equal(6m, ironPlate.CycleSeconds);
        Assert.Equal([RecipeIngredient.Of(ItemId.Of("Desc_IronIngot_C"), 3)], ironPlate.Inputs);
        Assert.Equal([RecipeIngredient.Of(ItemId.Of("Desc_IronPlate_C"), 2)], ironPlate.Outputs);
        Assert.False(ironPlate.IsAlternate);
    }

    // The game writes "6.000000". A decimal keeps that scale, so without care the API would answer 6.000000.
    [Fact]
    public async Task Cycle_times_from_the_game_file_keep_no_trailing_zeros()
    {
        var resources = await SatisfactoryImporter.Import("Fixtures/en-US.json");

        Recipe ironPlate = Assert.Single(SatisfactoryTranslator.TranslateToRecipes(resources), recipe => recipe.Id.Id == "Recipe_IronPlate_C");
        Assert.Equal("6", ironPlate.CycleSeconds.ToString(CultureInfo.InvariantCulture));
    }

    // The game stores fluid amounts in litres (x1000); the catalog uses cubic metres, like the game's UI.
    [Fact]
    public async Task Fluid_amounts_from_the_game_file_are_in_cubic_metres()
    {
        var resources = await SatisfactoryImporter.Import("Fixtures/en-US.json");

        Recipe plastic = Assert.Single(SatisfactoryTranslator.TranslateToRecipes(resources), recipe => recipe.Id.Id == "Recipe_Plastic_C");
        Assert.Equal([RecipeIngredient.Of(ItemId.Of("Desc_LiquidOil_C"), 3)], plastic.Inputs);
        Assert.Equal(
            [RecipeIngredient.Of(ItemId.Of("Desc_Plastic_C"), 2), RecipeIngredient.Of(ItemId.Of("Desc_HeavyOilResidue_C"), 1)],
            plastic.Outputs);
    }
}