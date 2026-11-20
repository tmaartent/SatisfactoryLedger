namespace SatisfactoryLedger.Mcp.LedgerApi;

/// <summary>A rate per minute. The API also gives it exactly (ADR-0004); the model reads the decimal.</summary>
internal sealed record RateResponse(double PerMinute);
