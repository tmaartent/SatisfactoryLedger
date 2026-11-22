namespace SatisfactoryLedger.Mcp.LedgerApi;

/// <summary>One step of a trace: machines short of an item, or that can't get it away, by how much per minute.</summary>
internal sealed record TraceStepResponse(IReadOnlyList<TraceGroupResponse> Machines, string Item, double PerMinute);
