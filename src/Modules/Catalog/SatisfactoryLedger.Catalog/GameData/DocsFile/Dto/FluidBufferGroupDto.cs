using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.GameData.DocsFile.Dto;

/// <summary>The section of fluid buffers (FGBuildablePipeReservoir): their inventory size (BR-A7).</summary>
internal class FluidBufferGroupDto : GameDataGroupDto
{
    [JsonPropertyName("Classes")]
    public List<StorageDto> Storages { get; set; } = [];
}
