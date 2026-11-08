using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.GameData.DocsFile;

internal class ItemGroupDto: GameDataGroupDto
{
    [JsonPropertyName("Classes")]
    public virtual List<ItemDto> Items { get; set; } = [];
}