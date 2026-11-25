using SatisfactoryLedger.World.Contracts.Arrangement;
using SatisfactoryLedger.World.Domain.Arrangement;

namespace SatisfactoryLedger.World.Arrangement;

/// <summary>
/// The plots laid over the world (decided 2026-11-12), in the game's centimetres: from the area's lowest corner, a plot
/// and its road every <see cref="PitchX"/> along x and <see cref="PitchY"/> along y. A cell of the arrangement is one
/// plot with the road after it, so factories on neighbouring cells have a road between them. On footprints (decided
/// 2026-11-25) a cell is one foundation and the grid has no roads: the roads are the problem's gaps.
/// </summary>
internal sealed record ArrangementGrid(double OriginX, double OriginY, double PitchX, double PitchY, double RoadX, double RoadY)
{
    public static ArrangementGrid Of(double originX, double originY, ArrangementSettings settings) => settings.Footprints
        ? new(originX, originY, BeltFloor.FoundationMetres * 100, BeltFloor.FoundationMetres * 100, 0, 0)
        : new(
        originX,
        originY,
        (settings.PlotWidthMetres + settings.RoadXMetres) * 100,
        (settings.PlotDepthMetres + settings.RoadYMetres) * 100,
        settings.RoadXMetres * 100,
        settings.RoadYMetres * 100);

    /// <summary>Where a point of the world lies on the grid, in plots.</summary>
    public (double X, double Y) CellOf(double x, double y) => ((x - OriginX) / PitchX, (y - OriginY) / PitchY);

    /// <summary>A rectangle of plots back in the world, in centimetres: from its first plot to the end of its last, without the road after it.</summary>
    public WorldRectangle WorldOf(GridRectangleDocument plots) => new(
        OriginX + (plots.X * PitchX),
        OriginY + (plots.Y * PitchY),
        OriginX + ((plots.X + plots.Width) * PitchX) - RoadX,
        OriginY + ((plots.Y + plots.Depth) * PitchY) - RoadY);

    /// <summary>The middle of a square of the belt floor laid over these plots, in centimetres.</summary>
    public WorldPoint WorldOf(FloorSquareDocument square, BeltFloor floor) => new(
        OriginX + ((square.X + 0.5) * PitchX / floor.SquaresPerPlotX),
        OriginY + ((square.Y + 0.5) * PitchY / floor.SquaresPerPlotY));

    /// <summary>How many plots fit in a stretch of the world, with the roads between them.</summary>
    public (int Width, int Depth) PlotsIn(double width, double depth) =>
        (Math.Max(1, (int)Math.Floor((width + RoadX) / PitchX)), Math.Max(1, (int)Math.Floor((depth + RoadY) / PitchY)));

    /// <summary>
    /// The plot a factory takes: one, whatever its machines' footprints add up to, where their middle is (decided
    /// 2026-11-18: every factory is built to fit a plot, on several floors where it needs more).
    /// </summary>
    public GridRectangle PlotOf(IReadOnlyCollection<(double X, double Y)> outline)
    {
        (double minX, double minY, double maxX, double maxY) = Bounds(outline);
        (double x, double y) = CellOf((minX + maxX) / 2, (minY + maxY) / 2);
        return new GridRectangle((int)Math.Floor(x), (int)Math.Floor(y), 1, 1);
    }

    /// <summary>The squares of the belt floor laid over these plots that something reaches into.</summary>
    public GridRectangle SquaresTouching(IReadOnlyCollection<(double X, double Y)> outline, BeltFloor floor)
    {
        (double minX, double minY, double maxX, double maxY) = Bounds(outline);
        (double sizeX, double sizeY) = (PitchX / floor.SquaresPerPlotX, PitchY / floor.SquaresPerPlotY);
        int x = (int)Math.Floor((minX - OriginX) / sizeX);
        int y = (int)Math.Floor((minY - OriginY) / sizeY);
        return new GridRectangle(
            x, y, Math.Max(1, (int)Math.Ceiling((maxX - OriginX) / sizeX) - x), Math.Max(1, (int)Math.Ceiling((maxY - OriginY) / sizeY) - y));
    }

    /// <summary>The plots something that stays touches: every plot it reaches into.</summary>
    public GridRectangle PlotsTouching(IReadOnlyCollection<(double X, double Y)> outline)
    {
        (double minX, double minY, double maxX, double maxY) = Bounds(outline);
        (double x0, double y0) = CellOf(minX, minY);
        (double x1, double y1) = CellOf(maxX, maxY);
        int x = (int)Math.Floor(x0);
        int y = (int)Math.Floor(y0);
        return new GridRectangle(x, y, Math.Max(1, (int)Math.Ceiling(x1) - x), Math.Max(1, (int)Math.Ceiling(y1) - y));
    }

    private static (double MinX, double MinY, double MaxX, double MaxY) Bounds(IReadOnlyCollection<(double X, double Y)> points) =>
        (points.Min(point => point.X), points.Min(point => point.Y), points.Max(point => point.X), points.Max(point => point.Y));
}
