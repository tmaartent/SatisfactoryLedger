namespace SatisfactoryLedger.World.Contracts.Analysis;

/// <summary>
/// Published by World when a snapshot needs its analysis (ADR-0018): after an import, or at startup for a snapshot
/// without a current analysis. Only the id travels; the analysis service fetches the flow network over HTTP.
/// </summary>
public sealed record SnapshotImported(Guid SnapshotId);
