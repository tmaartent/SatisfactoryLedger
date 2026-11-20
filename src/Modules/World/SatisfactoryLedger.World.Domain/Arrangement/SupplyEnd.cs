namespace SatisfactoryLedger.World.Domain.Arrangement;

/// <summary>A source or a consumer of one item on the belt floor: a factory or a spot, and how much a minute.</summary>
public sealed record SupplyEnd(string Id, double PerMinute);
