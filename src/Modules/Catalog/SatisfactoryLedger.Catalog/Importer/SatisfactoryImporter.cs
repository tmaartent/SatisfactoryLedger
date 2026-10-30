using System.Text.Json;

using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SatisfactoryLedger.Catalog.Importer;

internal class SatisfactoryImporter
{
    // The NativeClass values registered with [JsonDerivedType] on GameDataGroupDto: the sections we import.
    private static readonly HashSet<string> SupportedNativeClasses =
        JsonSerializerOptions.Default.GetTypeInfo(typeof(GameDataGroupDto))
            .PolymorphismOptions!.DerivedTypes
            .Select(derivedType => (string)derivedType.TypeDiscriminator!)
            .ToHashSet(StringComparer.Ordinal);

    // STJ throws on an unknown type discriminator, so sections we don't import are dropped before deserializing.
    private static List<GameDataGroupDto> DeserializeSupportedGroups(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Expected the game data file to contain a JSON array of groups");
        }

        return
        [
            .. document.RootElement.EnumerateArray()
                .Where(IsSupportedGroup)
                .Select(group => group.Deserialize<GameDataGroupDto>()!)
        ];
    }

    private static bool IsSupportedGroup(JsonElement group)
    {
        return group.TryGetProperty("NativeClass", out JsonElement nativeClass)
               && SupportedNativeClasses.Contains(nativeClass.GetString() ?? string.Empty);
    }

    private static IEnumerable<T> ToEnumerable<T>(IEnumerator<T> enumerator)
    {
        while (enumerator.MoveNext())
        {
            yield return enumerator.Current;
        }
    }
    
    internal static async Task<List<GameDataGroupDto>> Import(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File not found", filePath);
        }

        string jsonString = await File.ReadAllTextAsync(filePath);

        List<GameDataGroupDto> dataList = DeserializeSupportedGroups(jsonString);

        var itemGroup = dataList.OfType<ItemGroupDto>().ToList();
        var items = itemGroup
            .SelectMany(dto => dto.Items)
            .ToDictionary(r => r.ClassName, r => r);
        
        var buildingGroup = dataList.OfType<BuildingGroupDto>().ToList();
        var buildings = buildingGroup
            .SelectMany(dto => dto.Buildings)
            .ToDictionary(r => r.ClassName, r => r);
        
        var recipeGroup = dataList.OfType<RecipeGroupDto>().ToList();
        var recipes = recipeGroup.SelectMany(dto => dto.Recipes);

        if (itemGroup.Count == 0 || recipeGroup.Count == 0 || buildingGroup.Count == 0)
        {
            throw new JsonException("Could not parse the required Resource-, Item-, Building- or Recipe-groups in the JSON file");
        }

        foreach (RecipeDto recipe in recipes)
        {
            // Inputs
            foreach (RecipeIngredientDto input in recipe.Ingredients)
            {
                input.Item = items.TryGetValue(input.ClassName, out ItemDto? resource)
                    ? resource
                    : throw new JsonException($"Item {input.ClassName} not found");
            }

            // Outputs
            foreach (RecipeIngredientDto output in recipe.Products)
            {
                output.Item = items.TryGetValue(output.ClassName, out ItemDto? itemDto)
                    ? itemDto
                    : throw new JsonException($"Item {output.ClassName} not found");
            }

            recipe.RecipeBuilding?.Building = buildings.TryGetValue(recipe.RecipeBuilding.ClassName, out BuildingDto? matchedResource)
                    ? matchedResource
                    : throw new JsonException($"Building  {recipe.RecipeBuilding.ClassName} not found");
        }

        return dataList;
    }

}