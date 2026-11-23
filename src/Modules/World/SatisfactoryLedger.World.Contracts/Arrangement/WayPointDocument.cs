namespace SatisfactoryLedger.World.Contracts.Arrangement;

/// <summary>A point of an existing belt's way, seen from above, in the game's centimetres.</summary>
/// <param name="Z">How high it is; null in runs from before heights were kept (2026-11-23), drawn on the floor.</param>
public sealed record WayPointDocument(double X, double Y, double? Z = null);
