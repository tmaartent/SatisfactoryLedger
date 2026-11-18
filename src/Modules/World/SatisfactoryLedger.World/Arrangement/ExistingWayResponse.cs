namespace SatisfactoryLedger.World.Arrangement;

/// <summary>
/// An existing belt outside the arranged area, from a node's extractor, a station or a drone port to where it comes into
/// the area: a routed belt that leaves the floor there joins it (decided 2026-11-18).
/// </summary>
/// <param name="Points">Its way, in the game's centimetres, from the building to where it comes in.</param>
internal sealed record ExistingWayResponse(string SpotId, IReadOnlyList<WorldPoint> Points);
