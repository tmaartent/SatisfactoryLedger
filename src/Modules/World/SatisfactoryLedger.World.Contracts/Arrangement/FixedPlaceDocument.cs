namespace SatisfactoryLedger.World.Contracts.Arrangement;

/// <param name="Blocks">Whether factories must keep off it: a station or drone port; not a node.</param>
public sealed record FixedPlaceDocument(string Id, GridRectangleDocument Space, bool Blocks);
