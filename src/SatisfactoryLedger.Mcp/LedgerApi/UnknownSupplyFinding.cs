namespace SatisfactoryLedger.Mcp.LedgerApi;

/// <summary>A station whose supply the save doesn't say: what it was seen to deliver, per item per minute.</summary>
internal sealed record UnknownSupplyFinding(string Id, string ClassName, IReadOnlyDictionary<string, double> Delivered, PositionDocument? Position);
