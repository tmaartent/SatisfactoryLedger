namespace SatisfactoryLedger.Catalog.Importer;

internal class ClearanceDto
{
    public string ClassName { get; set; } = string.Empty;

    /// <summary>The game's text, e.g. "((ClearanceBox=(Min=(X=-400.000000,…),Max=(…),IsValid=True)))".</summary>
    public string ClearanceData { get; set; } = string.Empty;
}