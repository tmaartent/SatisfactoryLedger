namespace SatisfactoryLedger.World.Contracts.Arrangement;

/// <summary>What one belt (or pipe) of a link carries: an item, per minute.</summary>
public sealed record BeltLoadDocument(string Item, double PerMinute, bool Pipe);
