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
/// <param name="SupplyLines">Whether belts are routed as supply lines (decided 2026-11-18): what brings an item runs past
/// the factories that take it, topped up by priority mergers, instead of a belt from each source to each.</param>
/// <param name="Footprints">Whether each factory takes the whole foundations its machines cover instead of one plot
/// (decided 2026-11-25): the cells are foundations, and the roads are the gaps kept between factories, so they stagger
/// instead of running in rows and columns. The plot sizes don't count then.</param>
internal sealed record ArrangementSettings(
    WorldRectangle? Area,
    double PlotWidthMetres,
    double PlotDepthMetres,
    double RoadXMetres,
    double RoadYMetres,
    int Together,
    double SpotWeight,
    double CeilingMetres,
    IReadOnlyList<WorldRectangle> Exclusions,
    bool SupplyLines = false,
    bool Footprints = false)
{
    /// <summary>
    /// The floor below the plots where the belts are routed, in squares of one foundation. On footprints a cell is one
    /// square, with no road of its own.
    /// </summary>
    public BeltFloor Floor => Footprints
        ? BeltFloor.Of(BeltFloor.FoundationMetres, BeltFloor.FoundationMetres, 0, 0, CeilingMetres)
        : BeltFloor.Of(PlotWidthMetres, PlotDepthMetres, RoadXMetres, RoadYMetres, CeilingMetres);

    /// <summary>
    /// The cells kept free between two factories: on footprints the roads, in foundations; on plots none, since they
    /// bring their roads.
    /// </summary>
    public (int X, int Y) Gaps => Footprints ? (BeltFloor.Foundations(RoadXMetres), BeltFloor.Foundations(RoadYMetres)) : (0, 0);
}
