using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.GameData.DocsFile;

internal class RecipeBuildingDto
{
    public string ClassName { get; set; } = string.Empty;
    
    [JsonIgnore]
    public BuildingDto? Building { get; set; }
}