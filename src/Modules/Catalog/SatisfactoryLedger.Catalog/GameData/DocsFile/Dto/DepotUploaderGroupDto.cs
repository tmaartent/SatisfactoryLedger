using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.GameData.DocsFile.Dto;

/// <summary>The section of the dimensional depot uploader (FGCentralStorageContainer): their inventory size (BR-A7).</summary>
internal class DepotUploaderGroupDto : GameDataGroupDto
{
    [JsonPropertyName("Classes")]
    public List<StorageDto> Storages { get; set; } = [];
}
