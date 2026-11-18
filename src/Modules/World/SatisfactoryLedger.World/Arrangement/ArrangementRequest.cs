namespace SatisfactoryLedger.World.Arrangement;

/// <param name="Area">The area to arrange, drawn on the map, in the game's centimetres; everything built when not given.</param>
/// <param name="PlotWidthMetres">A plot along x (east-west); 48 (6 foundations) when not given.</param>
/// <param name="PlotDepthMetres">A plot along y (north-south); 48 (6 foundations) when not given.</param>
/// <param name="RoadXMetres">The road between plots along x; 32 (4 foundations) when not given.</param>
/// <param name="RoadYMetres">The road between plots along y; 32 (4 foundations) when not given.</param>
/// <param name="Exclusions">Rectangles inside the area where no factory may stand, in the game's centimetres; none when
/// not given.</param>
/// <param name="Together">How strongly the arranged factories are kept together, in belt work per plot of spread; 2 when
/// not given, 0 not at all.</param>
/// <param name="SpotWeight">How much belts to nodes, stations and drone ports count in placing against a belt between
/// factories; 0.5 when not given, 0 not at all.</param>
/// <param name="CeilingMetres">How high the floor below the factories is, where their belts are routed; 32 (8
/// foundations of 4 metres) when not given.</param>
/// <param name="Seconds">How long the solver may take; 60 when not given.</param>
internal sealed record ArrangementRequest(
    WorldRectangle? Area, double? PlotWidthMetres, double? PlotDepthMetres, double? RoadXMetres, double? RoadYMetres, IReadOnlyList<WorldRectangle>? Exclusions, int? Together, double? SpotWeight, double? CeilingMetres, int? Seconds);
