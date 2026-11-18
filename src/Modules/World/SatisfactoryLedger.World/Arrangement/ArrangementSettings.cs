namespace SatisfactoryLedger.World.Arrangement;

/// <summary>
/// Where and on what to arrange factories (decided 2026-11-12): the area drawn on the map, divided into plots in rows
/// and columns with roads between them.
/// </summary>
/// <param name="Area">The area to arrange, in the game's centimetres: the factories inside it are arranged, and stay
/// inside it. Everything built when null.</param>
/// <param name="PlotWidthMetres">A plot's size along x (east-west).</param>
/// <param name="PlotDepthMetres">A plot's size along y (north-south).</param>
/// <param name="RoadXMetres">The road between two plots along x.</param>
/// <param name="RoadYMetres">The road between two plots along y.</param>
/// <param name="Together">How strongly the arranged factories are kept together: the belt work one plot of spread costs
/// (the width plus the depth of the rectangle around them).</param>
/// <param name="Exclusions">Rectangles drawn inside the area where no factory may stand, in the game's centimetres: the
/// plots they touch are kept free.</param>
internal sealed record ArrangementSettings(
    WorldRectangle? Area,
    double PlotWidthMetres,
    double PlotDepthMetres,
    double RoadXMetres,
    double RoadYMetres,
    int Together,
    IReadOnlyList<WorldRectangle> Exclusions);
