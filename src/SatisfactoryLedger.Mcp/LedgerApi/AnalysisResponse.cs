namespace SatisfactoryLedger.Mcp.LedgerApi;

/// <summary>A snapshot's findings, as far as the tools use them.</summary>
internal sealed record AnalysisResponse(
    AnalysisSummary Summary,
    IReadOnlyList<MachineFinding> Machines,
    IReadOnlyList<BottleneckFinding> Bottlenecks,
    IReadOnlyList<UnusedSupplyFinding> UnusedSupply,
    IReadOnlyList<UnknownSupplyFinding> UnknownSupply);
