using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.GameData.DocsFile.Dto;

/// <summary>The section of storage containers (FGBuildableStorage): their inventory size (BR-A7).</summary>
internal class StorageContainerGroupDto : GameDataGroupDto
{
    [JsonPropertyName("Classes")]
    public List<StorageDto> Storages { get; set; } = [];
}
