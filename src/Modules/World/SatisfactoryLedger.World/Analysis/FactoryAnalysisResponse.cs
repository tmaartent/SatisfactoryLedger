namespace SatisfactoryLedger.World.Analysis;

/// <summary>
/// The findings of one factory (BR-A14): its own machines and belts, and the suppliers that feed it. A supplier shared
/// with another factory is in both.
/// </summary>
internal sealed record FactoryAnalysisResponse(
    Guid FactoryId,
    string Name,
    bool Settled,
    AnalysisSummary Summary,
    IReadOnlyList<FactoryOutput> Output,
    IReadOnlyList<MachineFinding> Machines,
    IReadOnlyList<BottleneckFinding> Bottlenecks,
    IReadOnlyList<UnusedSupplyFinding> UnusedSupply,
    IReadOnlyList<UnknownSupplyFinding> UnknownSupply);