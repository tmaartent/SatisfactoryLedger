using System.Text.Json;

using SatisfactoryLedger.Catalog.Importer;

namespace SatisfactoryLedger.Catalog.Tests;

public class SatisfactoryImporterTests
{
    [Fact]
    public async Task Import_throws_if_file_does_not_exist()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(async () => await SatisfactoryImporter.Import("C:\\Foo.bar"));
        
    }

    [Fact]
    public async Task Import_parses_file_if_file_exists()
    {
        var resources = await SatisfactoryImporter.Import("Fixtures/Valid.json");
        
        Assert.NotNull(resources);
    }
    
    [Fact]
    public async Task Import_Throws_if_no_item_descriptor_group_exists()
    {
        await Assert.ThrowsAsync<JsonException>(async () => await SatisfactoryImporter.Import("Fixtures/NoItemGroup.json"));
    }
    
    [Fact]
    public async Task Import_Throws_if_recipe_group_does_not_exist()
    {
        await Assert.ThrowsAsync<JsonException>(async () => await SatisfactoryImporter.Import("Fixtures/NoRecipeGroup.json"));
    }

    [Fact]
    public async Task Import_links_input_and_output_resources()
    {
        var resources = await SatisfactoryImporter.Import("Fixtures/Valid.json");

        RecipeGroupDto? recipes = resources.OfType<RecipeGroupDto>().FirstOrDefault();
        Assert.NotNull(recipes);
        Assert.NotEmpty(recipes.Recipes);
        
        recipes?.Recipes
            .ForEach(dto =>
            {
                Assert.NotEmpty(dto.Ingredients);
                dto.Ingredients
                    .ForEach(ingredientDto =>
                        {
                            Assert.NotNull(ingredientDto.Item);
                            Assert.Equal(ingredientDto.ClassName, ingredientDto.Item.ClassName);
                        }
                    );
                
                Assert.NotEmpty(dto.Products);
                dto.Products
                    .ForEach(ingredientDto =>
                        {
                            Assert.NotNull(ingredientDto.Item);
                            Assert.Equal(ingredientDto.ClassName, ingredientDto.Item.ClassName);
                        }
                    );
            });
    }
    
    [Fact]
    public async Task Import_bundles_items_from_different_groups()
    {
        var resources = await SatisfactoryImporter.Import("Fixtures/Valid.json");

        List<ItemGroupDto> itemDescriptorGroupDtos = [.. resources.OfType<ItemGroupDto>()];
        Assert.NotNull(itemDescriptorGroupDtos);
        Assert.Equal(10, itemDescriptorGroupDtos.Count);
        
        List<RecipeGroupDto> recipeGroupDtos = [.. resources.OfType<RecipeGroupDto>()];
        Assert.NotNull(recipeGroupDtos);
        RecipeGroupDto recipeGroupDto = Assert.Single(recipeGroupDtos);
        
        Assert.Equal(4, recipeGroupDto.Recipes.Count);

        List<BuildingGroupDto> buildingGroupDtos = [.. resources.OfType<BuildingGroupDto>()];
        Assert.NotNull(buildingGroupDtos);
        Assert.Equal(2, buildingGroupDtos.Count);
        
        List<BeltGroupDto> beltGroupDtos = [.. resources.OfType<BeltGroupDto>()];
        Assert.NotNull(beltGroupDtos);
        Assert.Single(beltGroupDtos);
        
        List<PipeGroupDto> pipeGroupDtos = [.. resources.OfType<PipeGroupDto>()];
        Assert.NotNull(pipeGroupDtos);
        var item = Assert.Single(pipeGroupDtos);

        Assert.Equal(2, item.Tiers.Count);
        
        Assert.All(pipeGroupDtos[0].Tiers, pipe => Assert.False(pipe.ClassName.EndsWith("NoIndicator_C", StringComparison.InvariantCulture)));
    }

    [Fact]
    public async Task Import_Throws_if_building_group_does_not_exist()
    {
        await Assert.ThrowsAsync<JsonException>(async () => await SatisfactoryImporter.Import("Fixtures/NoBuildingGroup.json"));
    }

    [Fact]
    public async Task Import_links_buildings()
    {
        var resources = await SatisfactoryImporter.Import("Fixtures/Valid.json");

        RecipeGroupDto? recipes = resources.OfType<RecipeGroupDto>().FirstOrDefault();
        Assert.NotNull(recipes);
        Assert.NotEmpty(recipes.Recipes);

        recipes.Recipes
            .ForEach(dto =>
            {
                Assert.NotNull(dto.RecipeBuilding);

                Assert.NotNull(dto.RecipeBuilding.Building);
                Assert.Equal(dto.RecipeBuilding.ClassName, dto.RecipeBuilding.Building.ClassName);
            });
    }
    
    [Fact]
    public async Task Import_Throws_if_recipe_ingredient_cannot_be_found()
    {
        await Assert.ThrowsAsync<JsonException>(async () => await SatisfactoryImporter.Import("Fixtures/InvalidRecipeInput.json"));
    }
    
    [Fact]
    public async Task Import_Throws_if_recipe_product_cannot_be_found()
    {
        await Assert.ThrowsAsync<JsonException>(async () => await SatisfactoryImporter.Import("Fixtures/InvalidRecipeOutput.json"));
    }
    
    [Fact]
    public async Task Import_Throws_if_recipe_building_cannot_be_found()
    {
        await Assert.ThrowsAsync<JsonException>(async () => await SatisfactoryImporter.Import("Fixtures/InvalidRecipeBuilding.json"));
    }
    
    [Fact]
    public async Task Import_parses_utf16_file()
    {
        var utf16Resources = await SatisfactoryImporter.Import("Fixtures/Valid.utf16.json");
        
        Assert.NotNull(utf16Resources);
        
        var utf8Resources = await SatisfactoryImporter.Import("Fixtures/Valid.json");
        
        Assert.Equal(utf8Resources.Count, utf16Resources.Count);
    }
    
    [Fact]
    public async Task Import_ignores_sections_it_does_not_import()
    {
        var withUnsupportedSection = await SatisfactoryImporter.Import("Fixtures/UnsupportedSection.json");
        var withoutUnsupportedSection = await SatisfactoryImporter.Import("Fixtures/Valid.json");

        Assert.Equal(withoutUnsupportedSection.Count, withUnsupportedSection.Count);
    }

    // en-US.json is a snapshot of the game's Docs file. The expected counts belong to that snapshot.
    [Fact]
    public async Task Import_of_the_game_file_keeps_only_production_recipes()
    {
        var resources = await SatisfactoryImporter.Import("Fixtures/en-US.json");

        RecipeGroupDto recipeGroup = Assert.Single(resources.OfType<RecipeGroupDto>());
        Assert.Equal(291, recipeGroup.Recipes.Count);
    }

    [Fact]
    public async Task Import_of_the_game_file_finds_all_production_buildings()
    {
        var resources = await SatisfactoryImporter.Import("Fixtures/en-US.json");

        string[] expected =
        [
            "Build_AssemblerMk1_C", "Build_Blender_C", "Build_ConstructorMk1_C", "Build_Converter_C",
            "Build_FoundryMk1_C", "Build_HadronCollider_C", "Build_ManufacturerMk1_C", "Build_OilRefinery_C",
            "Build_Packager_C", "Build_QuantumEncoder_C", "Build_SmelterMk1_C"
        ];
        string[] actual =
        [
            .. resources.OfType<BuildingGroupDto>()
                .SelectMany(group => group.Buildings)
                .Select(building => building.ClassName)
                .Order(StringComparer.Ordinal)
        ];
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task Import_of_the_game_file_finds_all_transport_tiers()
    {
        var resources = await SatisfactoryImporter.Import("Fixtures/en-US.json");

        BeltGroupDto beltGroup = Assert.Single(resources.OfType<BeltGroupDto>());
        Assert.Equal(6, beltGroup.Tiers.Count);

        PipeGroupDto pipeGroup = Assert.Single(resources.OfType<PipeGroupDto>());
        Assert.Equal(2, pipeGroup.Tiers.Count);
    }

    [Fact]
    public async Task Import_of_the_game_file_finds_all_items()
    {
        var resources = await SatisfactoryImporter.Import("Fixtures/en-US.json");

        int itemCount = resources.OfType<ItemGroupDto>().Sum(group => group.Items.Count);
        Assert.Equal(190, itemCount);
    }

    [Fact]
    public async Task Import_of_the_game_file_finds_all_raw_resources()
    {
        var resources = await SatisfactoryImporter.Import("Fixtures/en-US.json");

        string[] expected =
        [
            "Desc_Coal_C", "Desc_LiquidOil_C", "Desc_NitrogenGas_C", "Desc_OreBauxite_C", "Desc_OreCopper_C",
            "Desc_OreGold_C", "Desc_OreIron_C", "Desc_OreUranium_C", "Desc_RawQuartz_C", "Desc_SAM_C",
            "Desc_Stone_C", "Desc_Sulfur_C", "Desc_Water_C"
        ];
        ResourceItemGroupDto resourceGroup = Assert.Single(resources.OfType<ResourceItemGroupDto>());
        string[] actual =
        [
            .. resourceGroup.Items
                .Select(item => item.ClassName)
                .Order(StringComparer.Ordinal)
        ];
        Assert.Equal(expected, actual);
    }
}