namespace SatisfactoryLedger.World.Domain.Tracing;

/// <summary>
/// Where a shortage or a blockage starts: why, of which item, at which buildables, and the steps that lead there from
/// the machines traced. <see cref="PerMinute"/> is what <see cref="At"/> make, offer, carry or take of the item: the
/// producers at full speed, a source's offer, a belt's capacity, the consumers at full speed.
/// </summary>
public sealed record TraceStart(TraceCause Cause, ItemId Item, IReadOnlyList<BuildableId> At, IReadOnlyList<TraceStep> Path, double PerMinute)
{
    /// <summary>Taken by others: who else gets the item, per minute. Supplied by others: who else sends it, per minute.</summary>
    public IReadOnlyDictionary<BuildableId, double> Others { get; init; } = new Dictionary<BuildableId, double>();

    /// <summary>Consumer starved: the inputs the consumers are short of, with the share they get.</summary>
    public IReadOnlyDictionary<ItemId, double> ShortOf { get; init; } = new Dictionary<ItemId, double>();
}
