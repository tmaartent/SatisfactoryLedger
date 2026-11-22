namespace SatisfactoryLedger.Mcp.LedgerApi;

/// <summary>A miner, extractor or station that could deliver more than is taken from it; rates per minute.</summary>
internal sealed record UnusedSupplyFinding(string Id, string ClassName, string Item, double Offered, double Delivered, PositionDocument? Position);
