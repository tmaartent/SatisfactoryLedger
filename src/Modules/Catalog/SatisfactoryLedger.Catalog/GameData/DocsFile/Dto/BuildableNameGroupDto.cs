namespace SatisfactoryLedger.Catalog.GameData.DocsFile.Dto;

/// <summary>
/// Every buildable class in the Docs file with its display name, from any section: splitters, junctions and sinks have
/// no section the Catalog imports. The importer collects them itself, so it isn't registered as a [JsonDerivedType].
/// </summary>
internal class BuildableNameGroupDto : GameDataGroupDto
{
    public List<BuildableNameDto> Classes { get; set; } = [];
}
