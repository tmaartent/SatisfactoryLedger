namespace SatisfactoryLedger.World.Contracts.Analysis;

/// <summary>What the analysis service sends World for a snapshot (ADR-0018): the analysis, and how long the simulation took.</summary>
public sealed record AnalysisResult(TimeSpan Duration, AnalysisDocument Analysis);
