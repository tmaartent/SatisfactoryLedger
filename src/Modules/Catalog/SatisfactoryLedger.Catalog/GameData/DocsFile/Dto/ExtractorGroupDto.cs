using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.GameData.DocsFile.Dto;

/// <summary>
/// The game puts extractors in three sections. STJ can register a type for only one discriminator,
/// so each section gets its own subclass.
/// </summary>
internal abstract class ExtractorGroupDto : GameDataGroupDto
{
    [JsonPropertyName("Classes")]
    public List<ExtractorDto> Extractors { get; set; } = [];
}