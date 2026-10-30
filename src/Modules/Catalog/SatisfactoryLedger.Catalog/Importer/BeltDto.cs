using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.Importer;

internal class BeltDto: TransportTierDto
{
    [JsonPropertyName("mSpeed")]
    public string Speed { get; set; } = string.Empty;
}