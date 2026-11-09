using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.GameData.DocsFile.Dto;

internal class RecipeDto
{
    [JsonPropertyName("ClassName")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("mDisplayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("mManufactoringDuration")]
    public string ManufactoringDuration { get; set; } = string.Empty;

    [JsonPropertyName("mProducedIn")]
    [JsonConverter(typeof(Json.SatisfactoryBuildingConverter))]
    public RecipeBuildingDto? RecipeBuilding { get; set; }
    
    [JsonPropertyName("mIngredients")]
    [JsonConverter(typeof(Json.SatisfactoryIngredientConverter))]
    public List<RecipeIngredientDto> Ingredients { get; set; } = [];
    
    [JsonPropertyName("mProduct")]
    [JsonConverter(typeof(Json.SatisfactoryIngredientConverter))]
    public List<RecipeIngredientDto> Products { get; set; } = [];
}