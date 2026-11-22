namespace SatisfactoryLedger.Mcp.LedgerApi;

/// <summary>Something the player places: its class name as <see cref="Id"/>, and the name the game shows.</summary>
internal sealed record BuildableResponse(string Id, string Name);
