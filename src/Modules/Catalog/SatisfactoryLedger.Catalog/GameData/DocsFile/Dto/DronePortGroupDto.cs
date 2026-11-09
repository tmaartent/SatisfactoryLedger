using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.GameData.DocsFile.Dto;

/// <summary>The drone port's section: what a drone's trip costs (BR-A16).</summary>
internal class DronePortGroupDto : GameDataGroupDto
{
    [JsonPropertyName("Classes")]
    public List<DronePortDto> DronePorts { get; set; } = [];
}