using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.Importer;

internal class TransportTierGroupDto<TTier> : GameDataGroupDto
    where TTier : TransportTierDto
{
    [JsonPropertyName("Classes")]
    public List<TTier> Tiers
    {
        get;
        set
        {
            field = value?
                .Where(b => !b.ClassName.EndsWith("NoIndicator_C", StringComparison.InvariantCulture))
                .ToList() ?? [];
        }
    } = [];
}