namespace SatisfactoryLedger.World.Contracts.Arrangement;

/// <summary>Where a node's extractor, a station or a drone port stands on the belt floor, in its squares.</summary>
/// <param name="Entry">For one outside the area, where its existing belt comes into the area, in squares: a belt to it
/// leaves the floor there (decided 2026-11-18). Null when it has none, or in runs from before.</param>
/// <param name="Way">That existing belt's way outside the area, from the building to where it comes in.</param>
public sealed record SpotDocument(
    string Id, GridRectangleDocument Squares, GridRectangleDocument? Entry = null, IReadOnlyList<WayPointDocument>? Way = null);
