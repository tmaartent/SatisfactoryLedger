using System.Diagnostics;

using SatisfactoryLedger.World.Contracts.Arrangement;
using SatisfactoryLedger.World.Domain.Arrangement;

namespace SatisfactoryLedger.World.Simulation;

/// <summary>The analysis service's side of an arrangement: the problem document in, the proposal document out.</summary>
public static class ArrangementDocuments
{
    public static ArrangementProposalDocument Solve(ArrangementProblemDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var problem = new ArrangementProblem(
            [.. document.Factories.Select(factory => new ArrangedFactory(factory.Id, Rectangle(factory.Now)))],
            [.. document.Fixed.Select(place => new FixedPlace(place.Id, Rectangle(place.Space), place.Blocks))],
            [.. document.Links.Select(link => new ArrangementLink(link.From, link.To, link.Belts))],
            Rectangle(document.Area),
            document.GapX,
            document.GapY,
            document.Together);

        var watch = Stopwatch.StartNew();
        ArrangementProposal proposal = ArrangementSolver.Solve(problem, TimeSpan.FromSeconds(document.Seconds));
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
            watch.Elapsed);
    }

    private static GridRectangle Rectangle(GridRectangleDocument rectangle) => new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Depth);
}
