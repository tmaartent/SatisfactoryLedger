using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.GameData.DocsFile;

internal class BeltDto: TransportTierDto
{
    [JsonPropertyName("mSpeed")]
    public string Speed { get; set; } = string.Empty;
}