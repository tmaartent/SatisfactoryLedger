using SatisfactoryLedger.World.Analysis;
using SatisfactoryLedger.World.Persistence;
namespace SatisfactoryLedger.World.Snapshots;

/// <param name="Analysis">Whether its analysis is ready (ADR-0017): pending until the background worker has computed it.</param>
internal sealed record SnapshotResponse(
    Guid Id,
    string SessionName,
    int GameBuild,
    DateTimeOffset SavedAt,
    DateTimeOffset ImportedAt,
    SnapshotCounts Counts,
    IReadOnlyList<string> UnknownRecipes,
    IReadOnlyList<UnsupportedResponse> Unsupported,
    AnalysisStatus Analysis)
{
    public static SnapshotResponse From(StoredSnapshot snapshot, AnalysisStatus analysis) => new(
        snapshot.Id, snapshot.SessionName, snapshot.GameBuild, snapshot.SavedAt, snapshot.ImportedAt, snapshot.Counts,
        snapshot.UnknownRecipes,
        [.. snapshot.Unsupported.Select(unsupported => new UnsupportedResponse(unsupported.ClassName, unsupported.Count))],
        analysis);
}