namespace SatisfactoryLedger.Mcp.Answers;

/// <summary>A snapshot: which world, when it was saved, and whether its findings are ready ("ready", "pending" or "failed").</summary>
internal sealed record SnapshotAnswer(string Id, string World, DateTimeOffset SavedAt, string Analysis);
