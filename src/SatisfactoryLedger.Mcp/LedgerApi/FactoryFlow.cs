namespace SatisfactoryLedger.Mcp.LedgerApi;

/// <summary>An item a factory takes in or makes: now, at full speed, and how much is short; per minute.</summary>
internal sealed record FactoryFlow(string Item, double Now, double AtFullSpeed, double Short);
