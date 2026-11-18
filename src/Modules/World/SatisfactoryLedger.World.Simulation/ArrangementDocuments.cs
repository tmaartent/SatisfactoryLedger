using System.Diagnostics;

using SatisfactoryLedger.World.Contracts.Arrangement;
using SatisfactoryLedger.World.Domain.Arrangement;

namespace SatisfactoryLedger.World.Simulation;

/// <summary>
/// The analysis service's side of an arrangement: the problem document in, the proposal document out. The factories are
/// placed first, then their belts routed on the floor below them (decided 2026-11-18), when the problem has one.
/// </summary>
public static class ArrangementDocuments
{
    public static ArrangementProposalDocument Solve(ArrangementProblemDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var problem = new ArrangementProblem(
            [.. document.Factories.Select(factory => new ArrangedFactory(factory.Id, Rectangle(factory.Now)))],
            [.. document.Fixed.Select(place => new FixedPlace(place.Id, Rectangle(place.Space), place.Blocks))],
            [.. document.Links.Select(link => new ArrangementLink(link.From, link.To, link.Belts, link.WeightPercent, link.Pipes))],
            Rectangle(document.Area),
            document.GapX,
            document.GapY,
            document.Together);

        var watch = Stopwatch.StartNew();
        ArrangementProposal proposal = ArrangementSolver.Solve(problem, TimeSpan.FromSeconds(document.Seconds));
        // Without a layout there's nothing to route belts between.
        BeltRouting? routing = document.Floor is { } floor && proposal.Status != ArrangementStatus.NotFound
            ? BeltRouter.Route(
                new BeltFloor(floor.SquaresPerPlotX, floor.SquaresPerPlotY, floor.RoadSquaresX, floor.RoadSquaresY, floor.Levels),
                problem.Area,
                proposal.Places,
                problem.Links,
                (document.Spots ?? []).ToDictionary(spot => spot.Id, spot => Rectangle(spot.Squares)))
            : null;
        return new ArrangementProposalDocument(
            proposal.Places.ToDictionary(place => place.Key, place => new GridRectangleDocument(place.Value.X, place.Value.Y, place.Value.Width, place.Value.Depth)),
            proposal.Cost,
            proposal.CostNow,
            proposal.Status switch
            {
                ArrangementStatus.Optimal => ArrangementQuality.Optimal,
                ArrangementStatus.Feasible => ArrangementQuality.Feasible,
                _ => ArrangementQuality.NotFound,
            },
            watch.Elapsed,
            routing?.Belts.Select(belt => new RoutedBeltDocument(
                belt.From,
                belt.To,
                [.. belt.Corners.Select(corner => new FloorSquareDocument(corner.X, corner.Y))],
                belt.LeavesFloor,
                belt.Pipe,
                belt.Levels,
                [.. belt.Lifts.Select(lift => new FloorSquareDocument(lift.X, lift.Y))],
                belt.Index)).ToList(),
            routing?.Unrouted.Select(link => new ArrangementLinkDocument(link.From, link.To, link.Belts, link.WeightPercent, link.Pipes)).ToList(),
            routing?.TallestStack);
    }

    private static GridRectangle Rectangle(GridRectangleDocument rectangle) => new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Depth);
}
