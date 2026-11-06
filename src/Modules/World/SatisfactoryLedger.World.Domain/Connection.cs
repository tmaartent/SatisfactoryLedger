namespace SatisfactoryLedger.World.Domain;

/// <summary>Items move from <paramref name="From"/> to <paramref name="To"/>: an output connected to an input.</summary>
/// <param name="FromPort">Which output of a splitter it leaves from, counted from 0; null where that doesn't matter.</param>
/// <param name="ToPort">Which input of a merger it goes into, counted from 0; null where that doesn't matter.</param>
public sealed record Connection(BuildableId From, BuildableId To, int? FromPort = null, int? ToPort = null);
