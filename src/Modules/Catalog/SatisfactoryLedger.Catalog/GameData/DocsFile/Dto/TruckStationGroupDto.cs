using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.GameData.DocsFile.Dto;

/// <summary>The section of the truck station (FGBuildableDockingStation): their inventory size (BR-A7).</summary>
internal class TruckStationGroupDto : GameDataGroupDto
{
    [JsonPropertyName("Classes")]
    public List<StorageDto> Storages { get; set; } = [];
}
