using SatisfactoryLedger.World.Domain.Arrangement;

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
/// <param name="SpotWeight">How much belts to nodes, stations and drone ports count in placing, against a belt between
/// factories (decided 2026-11-18): 0 not at all, 1 as much.</param>
/// <param name="CeilingMetres">How high the floor below the factories is, where their belts run (decided 2026-11-18).</param>
/// <param name="Exclusions">Rectangles drawn inside the area where no factory may stand, in the game's centimetres: the
/// plots they touch are kept free.</param>
internal sealed record ArrangementSettings(
    WorldRectangle? Area,
    double PlotWidthMetres,
    double PlotDepthMetres,
    double RoadXMetres,
    double RoadYMetres,
    int Together,
    double SpotWeight,
    double CeilingMetres,
    IReadOnlyList<WorldRectangle> Exclusions)
{
    /// <summary>The floor below the plots where the belts are routed, in squares of one foundation.</summary>
    public BeltFloor Floor => BeltFloor.Of(PlotWidthMetres, PlotDepthMetres, RoadXMetres, RoadYMetres, CeilingMetres);
}
