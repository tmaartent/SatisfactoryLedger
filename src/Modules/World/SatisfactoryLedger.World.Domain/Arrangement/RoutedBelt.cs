namespace SatisfactoryLedger.World.Domain.Arrangement;

/// <summary>A belt or pipe of a link on the belt floor (decided 2026-11-18), from its lift under one factory to its lift under the other.</summary>
/// <param name="Corners">Where it starts, turns and ends, in squares of the floor; straight from one to the next.</param>
/// <param name="LeavesFloor">Whether one end is a spot outside the area: the belt leaves the floor at that corner.</param>
/// <param name="Pipe">Whether it's a pipe: pipes stack only with pipes, belts only with belts.</param>
/// <param name="Levels">Its level in the stacks on each straight run, from one corner to the next: a belt keeps its level
/// from turn to turn (decided 2026-11-18).</param>
/// <param name="Index">Which of its link's belts it is, from 0; or of its pipes, for a pipe.</param>
public sealed record RoutedBelt(string From, string To, IReadOnlyList<FloorSquare> Corners, bool LeavesFloor, bool Pipe, IReadOnlyList<int> Levels, int Index)
{
    /// <summary>Where it turns 4 metres or more up or down, on a lift; a change of 2 is a ramp (<see cref="BeltFloor.ClimbNeedsLift"/>).</summary>
    public IReadOnlyList<FloorSquare> Lifts =>
        [.. Enumerable.Range(1, Math.Max(0, Levels.Count - 1)).Where(run => BeltFloor.ClimbNeedsLift(Levels[run] - Levels[run - 1])).Select(run => Corners[run])];
}
