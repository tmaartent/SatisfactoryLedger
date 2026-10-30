using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.Importer;

internal class BuildingDto
{
    [JsonPropertyName("ClassName")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("mDisplayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("mProductionShardSlotSize")]
    public string ProductionShardSlotSize { get; set; } = string.Empty;
}