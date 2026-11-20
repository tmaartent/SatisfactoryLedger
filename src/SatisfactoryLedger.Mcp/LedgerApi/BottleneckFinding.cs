namespace SatisfactoryLedger.Mcp.LedgerApi;

/// <summary>A belt or pipe at its capacity while more is offered to it (BR-A12); rates per minute.</summary>
internal sealed record BottleneckFinding(string Id, string ClassName, double Capacity, double Flow, double OfferedMore, PositionDocument? Position);
