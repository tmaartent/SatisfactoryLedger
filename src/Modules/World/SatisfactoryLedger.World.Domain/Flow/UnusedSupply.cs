namespace SatisfactoryLedger.World.Domain.Flow;

/// <summary>What a provider could deliver on top of what it does (BR-A13).</summary>
public sealed record UnusedSupply(BuildableId Provider, ItemId Item, double Offered, double Delivered);