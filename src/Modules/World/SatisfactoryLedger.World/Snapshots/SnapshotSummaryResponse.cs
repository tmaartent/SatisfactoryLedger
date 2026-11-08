using SatisfactoryLedger.World.Analysis;
namespace SatisfactoryLedger.World.Snapshots;

internal sealed record SnapshotSummaryResponse(
    Guid Id, string SessionName, int GameBuild, DateTimeOffset SavedAt, DateTimeOffset ImportedAt, SnapshotCounts Counts, AnalysisStatus Analysis);