using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.World.Domain;

/// <summary>
/// Brings items into the network from outside: a miner, an extractor, or a train, truck or drone station that unloads.
/// Never part of a factory (BR-W1).
/// </summary>
/// <param name="offer">
/// What it offers per minute (BR-A2), or null when that isn't known, such as an extractor on a node the Catalog
/// doesn't know. The analysis then assumes it supplies what is asked (BR-A9).
/// </param>
public sealed class Provider(BuildableId id, ItemOffer? offer = null) : Buildable(id)
{
    public ItemOffer? Offer { get; } = offer;
}

/// <summary>An item, and how much of it a provider can deliver per minute.</summary>
public sealed record ItemOffer(ItemId Item, Rate PerMinute)
{
    /// <summary>BR-W9: the extraction rate (normal node, 100%) × the node's purity × the clock speed.</summary>
    public static ItemOffer Extracting(ItemId resource, Rate extractionRate, Purity purity, ClockSpeed clockSpeed) =>
        new(resource, extractionRate * clockSpeed * Multiplier(purity));

    private static decimal Multiplier(Purity purity) => purity switch
    {
        Purity.Impure => 0.5m,
        Purity.Normal => 1m,
        Purity.Pure => 2m,
        _ => throw new ArgumentOutOfRangeException(nameof(purity), purity, null),
    };
}

/// <summary>How rich a resource node is (BR-W9). A water extractor counts as normal.</summary>
public enum Purity
{
    Impure,
    Normal,
    Pure,
}
