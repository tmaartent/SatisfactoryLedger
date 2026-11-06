namespace SatisfactoryLedger.World.Domain;

/// <summary>
/// Takes items out of the network: an AWESOME Sink, a dimensional depot uploader, a power generator, or a station
/// that loads. Never part of a factory (BR-W8).
/// </summary>
/// <param name="burns">For a truck station or drone port that loads: the fuel it burns.</param>
public sealed class Consumer(BuildableId id, IReadOnlySet<ItemId>? burns = null) : Buildable(id)
{
    /// <summary>The fuel it burns: fuel coming in takes only what the line's other consumers leave (step 14).</summary>
    public IReadOnlySet<ItemId> Burns { get; } = burns ?? new HashSet<ItemId>();
}
