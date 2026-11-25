using System.Diagnostics;

using SatisfactoryLedger.World.Contracts.Arrangement;
using SatisfactoryLedger.World.Domain.Arrangement;

namespace SatisfactoryLedger.World.Simulation;

/// <summary>
/// The analysis service's side of an arrangement: the problem document in, the proposal document out. The factories are
/// placed first, then their belts routed on the floor below them (decided 2026-11-18), when the problem has one: the
/// links' belts, or with supply lines on, each item's lines (<see cref="SupplyLines"/>), planned now that the factories
/// stand where they'll be.
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
        Dictionary<(string From, string To), IReadOnlyList<BeltLoadDocument>> loads = [];
        foreach (ArrangementLinkDocument link in document.Links.Where(link => link.Loads is not null))
        {
            loads.TryAdd((link.From, link.To), link.Loads!);
        }

        // Placed, then routed. When belts are left over, or need last-resort crossings, placed again elsewhere, up to
        // Placements in all, keeping the layout that left the fewest, then needed the fewest crossings, then the least belt
        // work (steps 101 and 102, asked 2026-11-25): the routing of one layout always comes out the same, but another
        // layout may route cleanly. Each placement takes the solver's whole time.
        List<IReadOnlyDictionary<string, GridRectangle>> tried = [];
        (ArrangementProposal Proposal, BeltRouting? Routing, List<SupplyLeg>? Legs)? best = null;
        for (int seed = 0; seed < Placements; seed++)
        {
            ArrangementProposal placed = ArrangementSolver.Solve(problem, TimeSpan.FromSeconds(document.Seconds), tried, seed);
            if (placed.Status == ArrangementStatus.NotFound)
            {
                // No layout at all, or none left that differs from those tried.
                best ??= (placed, null, null);
                break;
            }

            tried.Add(placed.Places);
            (BeltRouting? routed, List<SupplyLeg>? routedLegs) = Route(document, problem, placed);
            if (best is not { } kept
                || (Left(routed), Crossings(routed), placed.Cost).CompareTo((Left(kept.Routing), Crossings(kept.Routing), kept.Proposal.Cost)) < 0)
            {
                best = (placed, routed, routedLegs);
            }

            if (Left(routed) == 0 && Crossings(routed) == 0)
            {
                break;
            }
        }

        (ArrangementProposal proposal, BeltRouting? routing, List<SupplyLeg>? legs) = best!.Value;

        // What a belt carries: its leg's load, for a supply line's; else its link's load at its number among the link's
        // belts, or among its pipes.
        BeltLoadDocument? LoadOf(RoutedBelt belt) => legs is not null
            ? legs.ElementAtOrDefault(belt.Index) is { } leg ? new BeltLoadDocument(leg.Item, leg.PerMinute, leg.Pipe, leg.Injection, leg.Overflow) : null
            : loads.GetValueOrDefault((belt.From, belt.To))?.Where(load => load.Pipe == belt.Pipe).ElementAtOrDefault(belt.Index);
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
                belt.Index,
                LoadOf(belt),
                belt.StartsOnBelt,
                belt.EndsOnBelt,
                [.. belt.LastResortCrossings.Select(square => new FloorSquareDocument(square.X, square.Y))])).ToList(),
            routing?.Unrouted.Select(link => new ArrangementLinkDocument(link.From, link.To, link.Belts, link.WeightPercent, link.Pipes)).ToList(),
            Math.Max(tried.Count, 1));
    }

    /// <summary>How many placements are tried at most when belts are left unrouted (step 101).</summary>
    private const int Placements = 3;

    /// <summary>How many belts and pipes a routing left over; none without one.</summary>
    private static int Left(BeltRouting? routing) => routing?.Unrouted.Sum(link => link.BeltsAndPipes) ?? 0;

    /// <summary>How many last-resort crossings a routing needed (step 102); none without one.</summary>
    private static int Crossings(BeltRouting? routing) => routing?.LastResortCrossings ?? 0;

    /// <summary>
    /// The belts of a layout, routed on the floor below it, when the problem has one: the links' belts, or with supply
    /// lines on, each item's lines, planned now that the factories stand where they'll be. Nothing without a layout.
    /// </summary>
    private static (BeltRouting? Routing, List<SupplyLeg>? Legs) Route(ArrangementProblemDocument document, ArrangementProblem problem, ArrangementProposal proposal)
    {
        if (document.Floor is not { } floorDocument || proposal.Status == ArrangementStatus.NotFound)
        {
            return (null, null);
        }

        var floor = new BeltFloor(floorDocument.SquaresPerPlotX, floorDocument.SquaresPerPlotY, floorDocument.RoadSquaresX, floorDocument.RoadSquaresY, floorDocument.Levels);
        Dictionary<string, GridRectangle> spots = (document.Spots ?? []).ToDictionary(spot => spot.Id, spot => Rectangle(spot.Squares));
        Dictionary<string, GridRectangle> entries = (document.Spots ?? [])
            .Where(spot => spot.Entry is not null)
            .ToDictionary(spot => spot.Id, spot => Rectangle(spot.Entry!));
        if (document.Supplies is { } supplies)
        {
            Dictionary<string, GridRectangle> ends = new(spots);
            foreach ((string id, GridRectangle place) in proposal.Places)
            {
                ends[id] = floor.SquaresUnder(place);
            }

            List<SupplyLeg> legs = Lines(supplies, ends);
            return (BeltRouter.RouteLines(floor, problem.Area, proposal.Places, legs, spots, entries), legs);
        }

        return (BeltRouter.Route(floor, problem.Area, proposal.Places, problem.Links, spots, entries), null);
    }

    /// <summary>
    /// Every item's supply lines, leg by leg, numbered across all items: a leg's <see cref="SupplyLeg.Onto"/> and
    /// <see cref="SupplyLeg.Off"/> count among all the legs, not only its item's.
    /// </summary>
    private static List<SupplyLeg> Lines(IReadOnlyList<ItemSupplyDocument> supplies, IReadOnlyDictionary<string, GridRectangle> ends)
    {
        List<SupplyLeg> legs = [];
        foreach (ItemSupplyDocument supply in supplies)
        {
            int before = legs.Count;
            legs.AddRange(SupplyLines.Plan(
                new ItemSupply(
                    supply.Item,
                    supply.Pipe,
                    supply.Capacity,
                    [.. supply.Sources.Select(source => new SupplyEnd(source.Id, source.PerMinute))],
                    [.. supply.Consumers.Select(consumer => new SupplyEnd(consumer.Id, consumer.PerMinute))]),
                ends).Select(leg => leg with { Onto = leg.Onto + before, Off = leg.Off + before }));
        }

        return legs;
    }

    private static GridRectangle Rectangle(GridRectangleDocument rectangle) => new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Depth);
}
