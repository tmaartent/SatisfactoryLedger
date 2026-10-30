using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.Importer;

internal class PipeDto: TransportTierDto
{
    [JsonPropertyName("mFlowLimit")]
    public string FlowLimit { get; set; } = string.Empty;
}