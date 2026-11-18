namespace SatisfactoryLedger.World.Contracts.Arrangement;

/// <summary>Where a node's extractor, a station or a drone port stands on the belt floor, in its squares.</summary>
public sealed record SpotDocument(string Id, GridRectangleDocument Squares);
