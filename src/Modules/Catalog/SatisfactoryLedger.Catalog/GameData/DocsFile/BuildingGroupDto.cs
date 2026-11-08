using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.GameData.DocsFile;

internal class BuildingGroupDto: GameDataGroupDto
{
    [JsonPropertyName("Classes")]
    public List<BuildingDto> Buildings { get; set; } = [];
}