using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.GameData.DocsFile;

internal class TransportTierDto
{
    [JsonPropertyName("ClassName")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("mDisplayName")]
    public string DisplayName { get; set; } = string.Empty;
}