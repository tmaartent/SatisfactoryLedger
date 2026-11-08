using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.GameData.DocsFile;

internal class GeneratorDto
{
    [JsonPropertyName("ClassName")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("mDisplayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Megawatts at 100% clock speed, e.g. "2500.000000".</summary>
    [JsonPropertyName("mPowerProduction")]
    public string PowerProduction { get; set; } = string.Empty;

    /// <summary>Litres of the supplemental fluid per megajoule, e.g. "1.600000"; "0.000000" when it needs none.</summary>
    [JsonPropertyName("mSupplementalToPowerRatio")]
    public string SupplementalToPowerRatio { get; set; } = "0";

    [JsonPropertyName("mFuel")]
    public List<GeneratorFuelDto> Fuels { get; set; } = [];
}