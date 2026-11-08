using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.GameData.DocsFile;

/// <summary>
/// One section of transport tiers. The clean ("NoIndicator") pipelines are kept: they carry as much as the
/// pipeline of the same mark, and blueprints use them, so saves contain them.
/// </summary>
internal class TransportTierGroupDto<TTier> : GameDataGroupDto
    where TTier : TransportTierDto
{
    [JsonPropertyName("Classes")]
    public List<TTier> Tiers { get; set; } = [];
}
