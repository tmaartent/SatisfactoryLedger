namespace SatisfactoryLedger.World.Contracts.Arrangement;

/// <summary>The floor below the factories where their belts run, in squares of one foundation laid over the plots.</summary>
/// <param name="SquaresPerPlotX">The squares a plot with the road after it takes along x.</param>
/// <param name="RoadSquaresX">The squares of that road.</param>
/// <param name="Levels">How many belts a stack holds.</param>
public sealed record BeltFloorDocument(int SquaresPerPlotX, int SquaresPerPlotY, int RoadSquaresX, int RoadSquaresY, int Levels);
