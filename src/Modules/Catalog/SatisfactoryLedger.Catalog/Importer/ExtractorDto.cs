using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.Importer;

internal class ExtractorDto
{
    [JsonPropertyName("ClassName")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("mDisplayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Items per cycle; for fluids in litres, like recipe amounts.</summary>
    [JsonPropertyName("mItemsPerCycle")]
    public string ItemsPerCycle { get; set; } = string.Empty;

    [JsonPropertyName("mExtractCycleTime")]
    public string ExtractCycleTime { get; set; } = string.Empty;

    /// <summary>For example "(RF_SOLID)" or "(RF_LIQUID,RF_GAS)".</summary>
    [JsonPropertyName("mAllowedResourceForms")]
    public string AllowedResourceForms { get; set; } = string.Empty;
}
