namespace SatisfactoryLedger.World.Arrangement;

/// <summary>A point in the world seen from above, in the game's centimetres.</summary>
/// <param name="Z">How high it is, where that's known: along an existing belt outside the arranged area, to draw it in 3D
/// as built (asked 2026-11-23). Null seen from above only.</param>
internal sealed record WorldPoint(double X, double Y, double? Z = null);
