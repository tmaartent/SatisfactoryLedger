using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.GameData.DocsFile.Dto;

internal class PumpDto
{
    [JsonPropertyName("ClassName")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("mDisplayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Its head lift in metres, as its description gives it, e.g. "20.000000"; a valve's is "0.000000". The owner chose
    /// it over <c>mMaxPressure</c> (22 m) on 2026-11-24.
    /// </summary>
    [JsonPropertyName("mDesignPressure")]
    public string DesignPressure { get; set; } = "0";
}
