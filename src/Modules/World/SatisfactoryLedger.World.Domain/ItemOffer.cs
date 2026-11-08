using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.World.Domain;

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