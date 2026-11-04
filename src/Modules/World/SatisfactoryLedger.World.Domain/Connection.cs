namespace SatisfactoryLedger.World.Domain;

/// <summary>Items move from <paramref name="From"/> to <paramref name="To"/>: an output connected to an input.</summary>
public sealed record Connection(BuildableId From, BuildableId To);
