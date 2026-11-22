namespace SatisfactoryLedger.Mcp.LedgerApi;

/// <summary>A trace of the machines of one class and recipe: how many, how many were traced, and where it starts.</summary>
internal sealed record TraceResponse(string Building, string? Recipe, int Machines, int Traced, IReadOnlyList<TraceStartResponse> Starts);
