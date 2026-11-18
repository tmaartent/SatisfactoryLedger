namespace SatisfactoryLedger.World.Contracts.Arrangement;

/// <summary>A run of the optimal arrangement was requested: the analysis service computes it (ADR-0018 pattern).</summary>
public sealed record ArrangementRequested(Guid ArrangementId);
