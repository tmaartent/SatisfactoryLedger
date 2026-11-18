namespace SatisfactoryLedger.World.Arrangement;

/// <summary>Where an existing belt crosses into the arranged area, and the way it takes outside it, from the building on.</summary>
internal sealed record ExistingWay(WorldPoint Entry, IReadOnlyList<WorldPoint> Outside);
