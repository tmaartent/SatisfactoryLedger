using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.Importer;

internal class RecipeIngredientDto
{
    public string ClassName { get; set; } = string.Empty;
    public int Amount { get; set; }
    
    [JsonIgnore]
    public ItemDto? Item { get; set; }
}