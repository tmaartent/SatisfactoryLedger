using SatisfactoryLedger.World.Contracts.Arrangement;
using SatisfactoryLedger.World.Domain.Arrangement;

namespace SatisfactoryLedger.World.Arrangement;

/// <summary>
/// The plots laid over the world (decided 2026-11-12), in the game's centimetres: from the area's lowest corner, a plot
/// and its road every <see cref="PitchX"/> along x and <see cref="PitchY"/> along y. A cell of the arrangement is one
/// plot with the road after it, so factories on neighbouring cells have a road between them.
/// </summary>
internal sealed record ArrangementGrid(double OriginX, double OriginY, double PitchX, double PitchY, double RoadX, double RoadY)
{
    public static ArrangementGrid Of(double originX, double originY, ArrangementSettings settings) => new(
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

    /// <summary>How many plots fit in a stretch of the world, with the roads between them.</summary>
    public (int Width, int Depth) PlotsIn(double width, double depth) =>
        (Math.Max(1, (int)Math.Floor((width + RoadX) / PitchX)), Math.Max(1, (int)Math.Floor((depth + RoadY) / PitchY)));

    /// <summary>The plots that take a stretch of the world: as many as its size needs, with the roads between them inside.</summary>
    public (int Width, int Depth) PlotsFor(double width, double depth) =>
        (Math.Max(1, (int)Math.Ceiling((width + RoadX) / PitchX)), Math.Max(1, (int)Math.Ceiling((depth + RoadY) / PitchY)));

    /// <summary>
    /// The plots a factory takes: as many as its outline needs, centred on where it stands, so it can move to any plots
    /// of that size.
    /// </summary>
    public GridRectangle PlotsFor(IReadOnlyCollection<(double X, double Y)> outline)
    {
        (double minX, double minY, double maxX, double maxY) = Bounds(outline);
        (int width, int depth) = PlotsFor(maxX - minX, maxY - minY);
        (double centreX, double centreY) = CellOf((minX + maxX) / 2, (minY + maxY) / 2);
        return new GridRectangle((int)Math.Round(centreX - (width / 2.0)), (int)Math.Round(centreY - (depth / 2.0)), width, depth);
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
