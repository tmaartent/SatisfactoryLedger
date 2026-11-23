using SatisfactoryLedger.World.Contracts.Arrangement;
using SatisfactoryLedger.World.Domain.Arrangement;

namespace SatisfactoryLedger.World.Arrangement;

/// <summary>An arrangement's problem as made from a world, with what the analysis service needs besides to route its belts.</summary>
/// <param name="Spots">Where the nodes' extractors, stations and drone ports stand, in squares.</param>
/// <param name="Loads">Per link, what each of its belts and pipes carries, in the order the router numbers them.</param>
/// <param name="Supplies">Per item, what brings it and what takes it, to route as supply lines; null without them.</param>
/// <param name="Entries">For spots outside the area, where their existing belts come into it.</param>
internal sealed record MadeArrangement(
    ArrangementProblem Problem,
    ArrangementGrid Grid,
    IReadOnlyDictionary<string, GridRectangle> Spots,
    IReadOnlyDictionary<(string From, string To), IReadOnlyList<BeltLoadDocument>> Loads,
    IReadOnlyList<ItemSupplyDocument>? Supplies,
    IReadOnlyDictionary<string, SpotEntry> Entries)
{
    /// <summary>The problem as the analysis service receives it, with how long the solver may take and the floor to route belts on.</summary>
    public ArrangementProblemDocument ToDocument(int seconds, BeltFloor floor)
    {
        ArgumentNullException.ThrowIfNull(floor);
        return new(
            [.. Problem.Factories.Select(factory => new ArrangedFactoryDocument(factory.Id, Document(factory.Now)))],
            [.. Problem.Fixed.Select(place => new FixedPlaceDocument(place.Id, Document(place.Space), place.Blocks))],
            [.. Problem.Links.Select(link => new ArrangementLinkDocument(
                link.From, link.To, link.Belts, link.WeightPercent, link.Pipes, Loads.GetValueOrDefault((link.From, link.To))))],
            Document(Problem.Area),
            Problem.GapX,
            Problem.GapY,
            Problem.Together,
            seconds,
            new BeltFloorDocument(floor.SquaresPerPlotX, floor.SquaresPerPlotY, floor.RoadSquaresX, floor.RoadSquaresY, floor.Levels),
            [.. Spots.Select(spot => Entries.GetValueOrDefault(spot.Key) is { } entry
                ? new SpotDocument(spot.Key, Document(spot.Value), Document(entry.Squares), [.. entry.Way.Select(point => new WayPointDocument(point.X, point.Y, point.Z))])
                : new SpotDocument(spot.Key, Document(spot.Value)))],
            Supplies);
    }

    private static GridRectangleDocument Document(GridRectangle rectangle) => new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Depth);
}
