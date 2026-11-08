namespace SatisfactoryLedger.World.Simulation;

public sealed record UnknownSupplyDocument(string Id, IReadOnlyDictionary<string, double> Delivered, double? StockMinutes, bool Confirmed);