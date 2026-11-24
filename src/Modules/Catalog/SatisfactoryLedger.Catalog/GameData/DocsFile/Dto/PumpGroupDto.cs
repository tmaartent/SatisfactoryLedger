using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.GameData.DocsFile.Dto;

/// <summary>The section of pipeline pumps (FGBuildablePipelinePump): their head lift. The valve is in it too.</summary>
internal class PumpGroupDto : GameDataGroupDto
{
    [JsonPropertyName("Classes")]
    public List<PumpDto> Pumps { get; set; } = [];
}
