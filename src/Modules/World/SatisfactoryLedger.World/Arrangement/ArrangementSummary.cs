using SatisfactoryLedger.World.Contracts.Arrangement;

namespace SatisfactoryLedger.World.Arrangement;

/// <summary>A run of the optimal arrangement: its settings, where it stands, and once ready what it saves.</summary>
/// <param name="CostNow">The belt work of the layout as built: per link, belts times cells between the centres.</param>
/// <param name="Cost">The belt work of the proposed layout.</param>
internal sealed record ArrangementSummary(
    Guid Id,
    Guid SnapshotId,
    string World,
    WorldRectangle? Area,
    IReadOnlyList<WorldRectangle> Exclusions,
    double PlotWidthMetres,
    double PlotDepthMetres,
    double RoadXMetres,
    double RoadYMetres,
    int Together,
    int Seconds,
    ArrangementRunStatus Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ComputedAt,
    TimeSpan? Duration,
    string? Error,
    double? CostNow,
    double? Cost,
    ArrangementQuality? Quality)
{
    public static ArrangementSummary From(StoredArrangement run)
    {
        ArrangementProposalDocument? proposal = run.Proposal is { } json ? ArrangementProposalDocument.FromJson(json) : null;
        return new ArrangementSummary(
            run.Id, run.SnapshotId, run.World, run.Area, run.Exclusions, run.PlotWidthMetres, run.PlotDepthMetres, run.RoadXMetres, run.RoadYMetres, run.Together, run.Seconds, run.Status,
            run.RequestedAt, run.ComputedAt, run.Duration, run.Error, proposal?.CostNow, proposal?.Cost, proposal?.Quality);
    }
}
