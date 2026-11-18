namespace SatisfactoryLedger.World.Domain.Arrangement;

/// <summary>
/// Where to place the factories so that the belt work between them is least (decided 2026-11-12): factories move, fixed
/// places don't, no two take the same space at any height, and between two factories there's at least a gap of
/// <see cref="GapX"/> cells along x or <see cref="GapY"/> along y. A layout costs, per link, the belts it needs times the
/// distance along the grid between the two centres.
/// </summary>
/// <param name="Area">Where factories may go.</param>
/// <param name="Together">How strongly the factories are kept together: the belt work one cell of spread costs, the
/// spread being the width plus the depth of the rectangle around them all. Not part of a layout's cost.</param>
public sealed record ArrangementProblem(
    IReadOnlyList<ArrangedFactory> Factories,
    IReadOnlyList<FixedPlace> Fixed,
    IReadOnlyList<ArrangementLink> Links,
    GridRectangle Area,
    int GapX,
    int GapY,
    int Together)
{
    /// <summary>What the layout as it stands costs.</summary>
    public double CostNow => CostOf(Factories.ToDictionary(factory => factory.Id, factory => factory.Now));

    /// <summary>What a layout costs, with the factories where <paramref name="places"/> puts them.</summary>
    public double CostOf(IReadOnlyDictionary<string, GridRectangle> places)
    {
        Dictionary<string, GridRectangle> all = Fixed.ToDictionary(place => place.Id, place => place.Space);
        foreach ((string id, GridRectangle place) in places)
        {
            all[id] = place;
        }

        return Links.Sum(link => link.Belts * (Distance(all[link.From], all[link.To]) / 2.0));
    }

    /// <summary>Twice the distance along the grid between the centres.</summary>
    private static long Distance(GridRectangle a, GridRectangle b) =>
        Math.Abs(a.DoubleCentreX - b.DoubleCentreX) + Math.Abs(a.DoubleCentreY - b.DoubleCentreY);
}
