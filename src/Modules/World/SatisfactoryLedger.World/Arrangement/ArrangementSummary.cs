using SatisfactoryLedger.World.Contracts.Arrangement;
using SatisfactoryLedger.World.Domain.Arrangement;

namespace SatisfactoryLedger.World.Arrangement;

/// <summary>A run of the optimal arrangement: its settings, where it stands, and once ready what it saves.</summary>
/// <param name="CostNow">The belt work of the layout as built: per link, belts times cells between the centres.</param>
/// <param name="Cost">The belt work of the proposed layout.</param>
/// <param name="BeltMetres">How long the routed belts and pipes are together, lifts not counted; null without routed belts.</param>
/// <param name="LevelsUsed">How many levels of the belt floor its belts use, up to the highest (2026-11-24: not the most
/// belts on one square, which said 5 where 3D showed 8 levels); null without routed belts or with belts from before levels.</param>
/// <param name="SupplyLines">Whether its belts were routed as supply lines.</param>
/// <param name="Footprints">Whether its factories took their footprints instead of one plot each.</param>
/// <param name="UnroutedBelts">How many belts and pipes couldn't be routed, not even with last-resort crossings.</param>
/// <param name="Placements">How many placements were tried: more than one when belts were left unrouted or needed
/// last-resort crossings (steps 101 and 102).</param>
/// <param name="LastResortCrossings">How many last-resort crossings its belts make together (step 102); null without a
/// belt floor or with belts from before.</param>
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
    double SpotWeight,
    double CeilingMetres,
    bool SupplyLines,
    bool Footprints,
    int Seconds,
    ArrangementRunStatus Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ComputedAt,
    TimeSpan? Duration,
    string? Error,
    double? CostNow,
    double? Cost,
    ArrangementQuality? Quality,
    double? BeltMetres,
    int? LevelsUsed,
    int? UnroutedBelts,
    int? Placements,
    int? LastResortCrossings)
{
    public static ArrangementSummary From(StoredArrangement run)
    {
        ArrangementProposalDocument? proposal = run.Proposal is { } json ? ArrangementProposalDocument.FromJson(json) : null;
        return new ArrangementSummary(
            run.Id, run.SnapshotId, run.World, run.Area, run.Exclusions, run.PlotWidthMetres, run.PlotDepthMetres, run.RoadXMetres, run.RoadYMetres, run.Together, run.SpotWeight, run.CeilingMetres, run.SupplyLines, run.Footprints, run.Seconds,
            run.Status, run.RequestedAt, run.ComputedAt, run.Duration, run.Error, proposal?.CostNow, proposal?.Cost, proposal?.Quality,
            MetresOf(run, proposal), LevelsOf(proposal), proposal?.Unrouted?.Sum(link => link.Belts + link.Pipes), proposal?.Placements,
            CrossingsOf(proposal));
    }

    private static int? CrossingsOf(ArrangementProposalDocument? proposal) =>
        proposal?.Belts is { } belts && belts.All(belt => belt.LastResortCrossings is not null)
            ? belts.Sum(belt => belt.LastResortCrossings!.Count)
            : null;

    private static int? LevelsOf(ArrangementProposalDocument? proposal) =>
        proposal?.Belts is { Count: > 0 } belts && belts.All(belt => belt.Levels is not null)
            ? BeltFloor.LevelsUsed(belts.Select(belt => belt.Levels!))
            : null;

    private static double? MetresOf(StoredArrangement run, ArrangementProposalDocument? proposal)
    {
        if (proposal?.Belts is not { } belts || run.Grid is not { } grid)
        {
            return null;
        }

        BeltFloor floor = run.Settings.Floor;
        double centimetres = belts.Sum(belt => belt.Corners
            .Select(corner => grid.WorldOf(corner, floor))
            .Zip(belt.Corners.Skip(1).Select(corner => grid.WorldOf(corner, floor)), (a, b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y))
            .Sum());
        return centimetres / 100;
    }
}
