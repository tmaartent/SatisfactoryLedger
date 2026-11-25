namespace SatisfactoryLedger.World.Arrangement;

/// <summary>A belt or pipe of a link on the floor below the factories, from its lift under one to its lift under the other.</summary>
/// <param name="Corners">Where it starts, turns and ends, in the game's centimetres; straight from one to the next.</param>
/// <param name="LeavesFloor">Whether its node, station or drone port stands outside the area: the belt leaves the floor at
/// that end.</param>
/// <param name="Pipe">Whether it's a pipe.</param>
/// <param name="Heights">How high it runs above the belt floor on each run from one corner to the next, in metres; a
/// change of 2 where it turns is a ramp. Empty for runs from before levels.</param>
/// <param name="Lifts">The corners where it changes 4 metres or more, on a lift.</param>
/// <param name="Levels">Its level on each run, from 0 on the floor: 1 on one belt, 2 on two. Empty for runs from before levels.</param>
/// <param name="Item">What it carries; null in runs from before loads.</param>
/// <param name="PerMinute">How much of it a minute.</param>
/// <param name="Injection">Whether it brings a second source into a supply line, by a priority merger where it ends.</param>
/// <param name="Overflow">Whether it carries a top-up's overflow on, from a smart splitter where it starts.</param>
/// <param name="StartsOnBelt">Whether it starts on another belt, at its smart splitter, instead of a lift up from below a
/// factory or spot.</param>
/// <param name="EndsOnBelt">Whether it ends on another belt, at its priority merger, instead of a lift up into a factory.</param>
/// <param name="LastResortCrossings">Where it crosses another belt where one of them turns, or parts from a stack swapped,
/// on another level, in the game's centimetres: only where it couldn't get through otherwise (step 102). Empty for runs
/// from before.</param>
internal sealed record RoutedBeltResponse(
    string From,
    string To,
    IReadOnlyList<WorldPoint> Corners,
    bool LeavesFloor,
    bool Pipe,
    IReadOnlyList<double> Heights,
    IReadOnlyList<WorldPoint> Lifts,
    IReadOnlyList<int> Levels,
    string? Item,
    double? PerMinute,
    bool Injection,
    bool Overflow,
    bool StartsOnBelt,
    bool EndsOnBelt,
    IReadOnlyList<WorldPoint> LastResortCrossings);
