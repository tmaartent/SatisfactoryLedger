namespace SatisfactoryLedger.World.Domain.Arrangement;

/// <summary>The belts between the arranged factories, as routed on the floor below them (decided 2026-11-18).</summary>
/// <param name="Unrouted">Per link, the belts that couldn't get through without crossing another or passing a lift.</param>
public sealed record BeltRouting(IReadOnlyList<RoutedBelt> Belts, IReadOnlyList<ArrangementLink> Unrouted);
