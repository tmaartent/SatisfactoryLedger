namespace SatisfactoryLedger.World.Contracts.Arrangement;

/// <summary>A belt or pipe of a link on the belt floor, from its lift under one factory to its lift under the other.</summary>
/// <param name="Corners">Where it starts, turns and ends; straight from one to the next.</param>
/// <param name="LeavesFloor">Whether one end is a spot outside the area, where the belt leaves the floor.</param>
/// <param name="Pipe">Whether it's a pipe.</param>
/// <param name="Levels">Its level in the stacks on each run from one corner to the next; none in runs from before levels.</param>
/// <param name="Lifts">The corners where it changes 4 metres or more, on a lift.</param>
/// <param name="Index">Which of its link's belts it is, from 0; or of its pipes, for a pipe.</param>
/// <param name="Load">What it carries; null in runs from before the service told it.</param>
/// <param name="StartsOnBelt">Whether it starts on another belt, at a smart splitter, instead of a lift.</param>
/// <param name="EndsOnBelt">Whether it ends on another belt, at a priority merger, instead of a lift.</param>
public sealed record RoutedBeltDocument(
    string From,
    string To,
    IReadOnlyList<FloorSquareDocument> Corners,
    bool LeavesFloor = false,
    bool Pipe = false,
    IReadOnlyList<int>? Levels = null,
    IReadOnlyList<FloorSquareDocument>? Lifts = null,
    int Index = 0,
    BeltLoadDocument? Load = null,
    bool StartsOnBelt = false,
    bool EndsOnBelt = false);
