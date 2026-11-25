namespace SatisfactoryLedger.World.Domain.Arrangement;

/// <summary>The belts between the arranged factories, as routed on the floor below them (decided 2026-11-18).</summary>
/// <param name="Unrouted">Per link, the belts that couldn't get through, not even with last-resort crossings.</param>
public sealed record BeltRouting(IReadOnlyList<RoutedBelt> Belts, IReadOnlyList<ArrangementLink> Unrouted)
{
    /// <summary>How many last-resort crossings its belts make together (step 102).</summary>
    public int LastResortCrossings => Belts.Sum(belt => belt.LastResortCrossings.Count);
}
