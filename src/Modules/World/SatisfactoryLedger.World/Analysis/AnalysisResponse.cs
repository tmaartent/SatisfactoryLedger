namespace SatisfactoryLedger.World.Analysis;

/// <summary>The findings of a snapshot: worst first in every list.</summary>
/// <param name="Settled">False when a part of the world didn't settle; its findings are marked approximate.</param>
/// <param name="PipeFlows">What flows through each pipe in the model; for the whole world only, empty for a factory.</param>
internal sealed record AnalysisResponse(
    bool Settled,
    AnalysisSummary Summary,
    GeneratorSummary GeneratorSummary,
    IReadOnlyList<ApproximatePart> ApproximateParts,
    IReadOnlyList<MachineFinding> Machines,
    IReadOnlyList<BottleneckFinding> Bottlenecks,
    IReadOnlyList<UnusedSupplyFinding> UnusedSupply,
    IReadOnlyList<UnknownSupplyFinding> UnknownSupply,
    IReadOnlyList<GeneratorFinding> Generators,
    IReadOnlyList<DronePortFinding> DronePorts,
    IReadOnlyList<PipeFlowFinding> PipeFlows);