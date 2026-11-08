using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.Importer;

/// <summary>
/// Generators come in two sections: nuclear, and the others that burn fuel (coal, fuel, biomass). STJ can register a
/// type for only one discriminator, so each section gets its own subclass. Geothermal burns nothing: not imported.
/// </summary>
internal abstract class GeneratorGroupDto : GameDataGroupDto
{
    [JsonPropertyName("Classes")]
    public List<GeneratorDto> Generators { get; set; } = [];
}