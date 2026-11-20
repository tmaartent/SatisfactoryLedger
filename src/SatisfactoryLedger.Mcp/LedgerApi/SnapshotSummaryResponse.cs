namespace SatisfactoryLedger.Mcp.LedgerApi;

/// <summary>A snapshot as the API lists it; only what the tools use.</summary>
internal sealed record SnapshotSummaryResponse(string Id, string SessionName, DateTimeOffset SavedAt, string Analysis);
