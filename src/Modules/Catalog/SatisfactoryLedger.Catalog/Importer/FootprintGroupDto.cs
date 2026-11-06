namespace SatisfactoryLedger.Catalog.Importer;

/// <summary>
/// Every class in the Docs file with clearance boxes, from any section: footprints don't belong to one native class.
/// The importer collects them itself, so it isn't registered as a [JsonDerivedType].
/// </summary>
internal class FootprintGroupDto : GameDataGroupDto
{
    public List<ClearanceDto> Classes { get; set; } = [];
}

internal class ClearanceDto
{
    public string ClassName { get; set; } = string.Empty;

    /// <summary>The game's text, e.g. "((ClearanceBox=(Min=(X=-400.000000,…),Max=(…),IsValid=True)))".</summary>
    public string ClearanceData { get; set; } = string.Empty;
}
