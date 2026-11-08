namespace SatisfactoryLedger.World.Snapshots;

/// <param name="FromPort">The splitter output it leaves from; null where that doesn't matter.</param>
/// <param name="ToPort">The merger input it enters by; null where that doesn't matter.</param>
internal sealed record ConnectionDocument(string From, string To, int? FromPort = null, int? ToPort = null);