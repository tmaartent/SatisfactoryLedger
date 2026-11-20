namespace SatisfactoryLedger.Mcp.LedgerApi;

/// <summary>An item of the Catalog: its class name as <see cref="Id"/>, and the name the game shows.</summary>
internal sealed record ItemResponse(string Id, string Name, string Form, bool IsRawResource);
