namespace SatisfactoryLedger.Mcp.LedgerApi;

/// <summary>Buildables of one class and recipe in a trace: how many, how much per minute (for others), the first ids and where the first stands.</summary>
internal sealed record TraceGroupResponse(string ClassName, string? Recipe, int Count, double PerMinute, IReadOnlyList<string> Ids, PositionDocument? Position);
