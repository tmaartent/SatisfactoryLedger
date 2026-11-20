namespace SatisfactoryLedger.World.Domain.Arrangement;

/// <summary>
/// What stays where it is (decided 2026-11-12): a resource node with its extractors, a train or truck station, a drone
/// port. Factories are drawn towards the ones that feed them.
/// </summary>
/// <param name="Blocks">
/// Whether it takes its space: a station or drone port does. A node doesn't: a factory may stand on platforms above it,
/// with the extractors below.
/// </param>
public sealed record FixedPlace(string Id, GridRectangle Space, bool Blocks);
