namespace SatisfactoryLedger.World.Domain.Arrangement;

/// <summary>
/// Where to place the factories so that the belt work between them is least (decided 2026-11-12): factories move, fixed
/// places don't, no two take the same space at any height, and between two factories there's at least a gap of
/// <see cref="GapX"/> cells along x or <see cref="GapY"/> along y. A layout costs, per link, the belts it needs times the
/// distance along the grid between the two centres, at the link's weight.
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
    /// <summary>The cells of the area no fixed place blocks: where factories may stand. A cell is a plot, or on footprints a foundation.</summary>
    public int FreeCells
    {
        get
        {
            bool[] blocked = new bool[Area.Width * Area.Depth];
            foreach (FixedPlace place in Fixed.Where(place => place.Blocks))
            {
                for (int y = Math.Max(place.Space.Y, Area.Y); y < Math.Min(place.Space.Y + place.Space.Depth, Area.Y + Area.Depth); y++)
                {
                    for (int x = Math.Max(place.Space.X, Area.X); x < Math.Min(place.Space.X + place.Space.Width, Area.X + Area.Width); x++)
                    {
                        blocked[((y - Area.Y) * Area.Width) + (x - Area.X)] = true;
                    }
                }
            }

            return blocked.Count(plot => !plot);
        }
    }

    /// <summary>Whether the factories could fit at all: there are at least as many free cells as they take.</summary>
    public bool HasRoom => Factories.Sum(factory => factory.Now.Width * factory.Now.Depth) <= FreeCells;

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

        return Links.Sum(link => link.BeltsAndPipes * (link.WeightPercent / 100.0) * (Distance(all[link.From], all[link.To]) / 2.0));
    }

    /// <summary>Twice the distance along the grid between the centres.</summary>
    private static long Distance(GridRectangle a, GridRectangle b) =>
        Math.Abs(a.DoubleCentreX - b.DoubleCentreX) + Math.Abs(a.DoubleCentreY - b.DoubleCentreY);
}
