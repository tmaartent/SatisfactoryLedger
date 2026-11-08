using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.World.Domain;

/// <summary>
/// Brings items into the network from outside: a miner, an extractor, or a train, truck or drone station that unloads.
/// Never part of a factory (BR-W1).
/// </summary>
/// <param name="offer">
/// What it offers per minute (BR-A2), or null when that isn't known, such as an extractor on a node the Catalog
/// doesn't know, or a station. The analysis then assumes it supplies what is asked (BR-A9).
/// </param>
/// <param name="stock">What it holds now, per item: what's on a platform, or what a drone port received.</param>
/// <param name="burns">The fuel a drone port or truck station burns.</param>
/// <param name="burnRate">For a drone port: how much of its fuel it burns per minute, when that's known (BR-A16).</param>
public sealed class Provider(
    BuildableId id,
    ItemOffer? offer = null,
    IReadOnlyDictionary<ItemId, decimal>? stock = null,
    IReadOnlySet<ItemId>? burns = null,
    Rate? burnRate = null) : Buildable(id)
{
    public ItemOffer? Offer { get; } = offer;

    /// <summary>
    /// The fuel it burns: fuel coming in takes only what the line's other consumers leave (step 14), unless its
    /// <see cref="BurnRate"/> is known.
    /// </summary>
    public IReadOnlySet<ItemId> Burns { get; } = burns ?? new HashSet<ItemId>();

    /// <summary>How much of its fuel it burns per minute; null when that isn't known (BR-A16).</summary>
    public Rate? BurnRate { get; } = burnRate;

    /// <summary>What it holds now, per item. Empty when it holds nothing or that isn't known. Decides confirmed supplied (BR-A9).</summary>
    public IReadOnlyDictionary<ItemId, decimal> Stock { get; } = stock ?? new Dictionary<ItemId, decimal>();
}