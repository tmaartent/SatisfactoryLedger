namespace SatisfactoryLedger.Mcp.LedgerApi;

/// <summary>One factory's findings in a snapshot, as far as the tools use them.</summary>
internal sealed record FactoryAnalysisResponse(
    string FactoryId,
    string Name,
    AnalysisSummary Summary,
    IReadOnlyList<FactoryFlow> Input,
    IReadOnlyList<FactoryFlow> Output,
    FactoryPower? Power,
    IReadOnlyList<MachineFinding> Machines);
