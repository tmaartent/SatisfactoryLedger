using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.World.Domain;

/// <summary>
/// Takes items out of the network: an AWESOME Sink, a dimensional depot uploader, a power generator, or a station
/// that loads. Never part of a factory (BR-W8), except a generator the player chose as one of its machines (BR-W2).
/// </summary>
/// <param name="burns">For a truck station or drone port that loads: the fuel it burns.</param>
/// <param name="generation">For a generator burning a known fuel: what it burns, needs and leaves (BR-A8).</param>
/// <param name="takesLeftoversOnly">
/// For a dimensional depot uploader: it takes only what the line's other consumers leave. In steady state a depot is
/// full (BR-A8).
/// </param>
/// <param name="burnRate">For a drone port: how much of its fuel it burns per minute, when that's known (BR-A16).</param>
public sealed class Consumer(
    BuildableId id,
    IReadOnlySet<ItemId>? burns = null,
    Generation? generation = null,
    bool takesLeftoversOnly = false,
    Rate? burnRate = null) : Buildable(id)
{
    public bool TakesLeftoversOnly { get; } = takesLeftoversOnly;

    /// <summary>
    /// The fuel it burns: fuel coming in takes only what the line's other consumers leave (step 14), unless its
    /// <see cref="BurnRate"/> is known.
    /// </summary>
    public IReadOnlySet<ItemId> Burns { get; } = burns ?? new HashSet<ItemId>();

    /// <summary>How much of its fuel it burns per minute; null when that isn't known (BR-A16).</summary>
    public Rate? BurnRate { get; } = burnRate;

    /// <summary>
    /// For a generator: the fuel it holds at save time, per item. A stock that lasts counts as supplied (BR-A17).
    /// </summary>
    public IReadOnlyDictionary<ItemId, decimal> InputStock { get; init; } = new Dictionary<ItemId, decimal>();

    /// <summary>
    /// Whether it's a power generator, whatever it burns: a factory can have it as one of its machines (BR-W2). The
    /// others (sinks, uploaders, stations) never belong to a factory (BR-W8).
    /// </summary>
    public bool IsGenerator { get; init; }

    /// <summary>Null for a sink, an uploader, a station, or a generator whose fuel isn't known: it takes everything.</summary>
    public Generation? Generation { get; } = generation;

    /// <summary>
    /// Its inventory, when the Catalog knows its size: at the end of a line it takes an item only while that fits
    /// (BR-A7). Null when its size isn't known; it then takes everything, as before.
    /// </summary>
    public Inventory? Inventory { get; init; }

}