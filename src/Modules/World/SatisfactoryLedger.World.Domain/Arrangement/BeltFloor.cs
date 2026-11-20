namespace SatisfactoryLedger.World.Domain.Arrangement;

/// <summary>
/// The floor below the factories where their belts run (decided 2026-11-18), in squares of one foundation laid over the
/// plots: a plot with the road after it is <see cref="SquaresPerPlotX"/> by <see cref="SquaresPerPlotY"/> squares.
/// </summary>
/// <param name="SquaresPerPlotX">The squares a plot with the road after it takes along x.</param>
/// <param name="RoadSquaresX">The squares of that road.</param>
/// <param name="Levels">How many belts a stack holds: one per 2 metres of ceiling, the stackable pole's step.</param>
public sealed record BeltFloor(int SquaresPerPlotX, int SquaresPerPlotY, int RoadSquaresX, int RoadSquaresY, int Levels)
{
    private const double FoundationMetres = 8;
    private const double LevelMetres = 2;
    private const double LiftFromMetres = 4;

    /// <summary>The floor under plots and roads given in metres, rounded to whole foundations, with a ceiling that high.</summary>
    public static BeltFloor Of(double plotWidthMetres, double plotDepthMetres, double roadXMetres, double roadYMetres, double ceilingMetres)
    {
        int perX = Math.Max(1, Foundations(plotWidthMetres + roadXMetres));
        int perY = Math.Max(1, Foundations(plotDepthMetres + roadYMetres));
        return new BeltFloor(
            perX,
            perY,
            Math.Clamp(Foundations(roadXMetres), 0, perX - 1),
            Math.Clamp(Foundations(roadYMetres), 0, perY - 1),
            Math.Max(1, (int)Math.Floor(ceilingMetres / LevelMetres)));
    }

    /// <summary>How high a level of a stack is above the belt floor, in metres: 2 a level, from 0 up.</summary>
    public static double HeightOf(int level) => level * LevelMetres;

    /// <summary>
    /// Whether a belt changing this many levels where it turns needs a lift there: 4 metres or more up or down does; 2
    /// is a ramp (decided 2026-11-18).
    /// </summary>
    public static bool ClimbNeedsLift(int levels) => Math.Abs(levels) * LevelMetres >= LiftFromMetres;

    /// <summary>The squares of a rectangle of plots, with the roads after them.</summary>
    public GridRectangle SquaresOf(GridRectangle plots) =>
        new(plots.X * SquaresPerPlotX, plots.Y * SquaresPerPlotY, plots.Width * SquaresPerPlotX, plots.Depth * SquaresPerPlotY);

    /// <summary>The squares under a factory on these plots: its plots and the roads between them, not the road after them.</summary>
    public GridRectangle SquaresUnder(GridRectangle plots) => new(
        plots.X * SquaresPerPlotX,
        plots.Y * SquaresPerPlotY,
        (plots.Width * SquaresPerPlotX) - RoadSquaresX,
        (plots.Depth * SquaresPerPlotY) - RoadSquaresY);

    // Away from zero: .NET rounds a half to even by default, so 2.5 foundations would be 2.
    private static int Foundations(double metres) => (int)Math.Round(metres / FoundationMetres, MidpointRounding.AwayFromZero);
}
