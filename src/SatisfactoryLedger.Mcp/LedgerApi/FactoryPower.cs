namespace SatisfactoryLedger.Mcp.LedgerApi;

/// <summary>What a factory's machines draw, now and at full speed.</summary>
internal sealed record FactoryPower(double NowMegawatts, double AtFullSpeedMegawatts);
