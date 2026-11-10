using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.GameData.DocsFile.Dto;

/// <summary>The section of freight platforms (FGBuildableTrainPlatformCargo): their inventory size (BR-A7).</summary>
internal class FreightPlatformGroupDto : GameDataGroupDto
{
    [JsonPropertyName("Classes")]
    public List<StorageDto> Storages { get; set; } = [];
}
