using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.Importer;

/// <summary>
/// The game puts extractors in three sections. STJ can register a type for only one discriminator,
/// so each section gets its own subclass.
/// </summary>
internal abstract class ExtractorGroupDto : GameDataGroupDto
{
    [JsonPropertyName("Classes")]
    public List<ExtractorDto> Extractors { get; set; } = [];
}

/// <summary>Miners and the oil extractor.</summary>
internal class ResourceExtractorGroupDto : ExtractorGroupDto;

internal class WaterPumpGroupDto : ExtractorGroupDto;

/// <summary>The resource well extractor. The pressurizer is a separate section: it extracts nothing itself.</summary>
internal class FrackingExtractorGroupDto : ExtractorGroupDto;
