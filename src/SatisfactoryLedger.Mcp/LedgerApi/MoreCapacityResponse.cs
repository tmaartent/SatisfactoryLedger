namespace SatisfactoryLedger.Mcp.LedgerApi;

/// <summary>What a bottleneck needs to carry all that's offered to it: lines side by side of a tier (its class name).</summary>
internal sealed record MoreCapacityResponse(string Tier, int Lines, double Carries);