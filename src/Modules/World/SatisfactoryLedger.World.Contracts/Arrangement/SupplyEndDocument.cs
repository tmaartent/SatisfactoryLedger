namespace SatisfactoryLedger.World.Contracts.Arrangement;

/// <summary>A source or a consumer of one item: a factory or a spot, and how much a minute.</summary>
public sealed record SupplyEndDocument(string Id, double PerMinute);
