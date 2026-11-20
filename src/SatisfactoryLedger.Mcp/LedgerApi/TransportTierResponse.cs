namespace SatisfactoryLedger.Mcp.LedgerApi;

/// <summary>A belt, lift or pipe tier, with its capacity per minute.</summary>
internal sealed record TransportTierResponse(string Id, string Name, string Form, int Capacity);
