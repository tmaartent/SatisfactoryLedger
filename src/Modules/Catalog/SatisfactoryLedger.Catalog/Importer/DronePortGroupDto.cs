using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.Importer;

/// <summary>The drone port's section: what a drone's trip costs (BR-A16).</summary>
internal class DronePortGroupDto : GameDataGroupDto
{
    [JsonPropertyName("Classes")]
    public List<DronePortDto> DronePorts { get; set; } = [];
}

internal class DronePortDto
{
    [JsonPropertyName("ClassName")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("mDisplayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Megajoules every round trip costs, e.g. "24000.000000".</summary>
    [JsonPropertyName("mTripPowerCost")]
    public string TripPowerCost { get; set; } = string.Empty;

    /// <summary>Megajoules per metre flown, e.g. "6.000000".</summary>
    [JsonPropertyName("mTripPowerPerMeterCost")]
    public string TripPowerPerMeterCost { get; set; } = "0";
}
