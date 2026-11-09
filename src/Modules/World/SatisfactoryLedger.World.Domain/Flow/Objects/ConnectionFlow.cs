namespace SatisfactoryLedger.World.Domain.Flow.Objects;

/// <summary>What flows over one connection in the steady state, per item and minute.</summary>
public sealed record ConnectionFlow(BuildableId From, BuildableId To, IReadOnlyDictionary<ItemId, double> Items);
