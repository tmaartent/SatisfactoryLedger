namespace SatisfactoryLedger.World.Analysis;

/// <summary>The answer while a snapshot's analysis isn't ready yet: ask again later (ADR-0017).</summary>
internal sealed record AnalysisPendingResponse(AnalysisStatus Status);