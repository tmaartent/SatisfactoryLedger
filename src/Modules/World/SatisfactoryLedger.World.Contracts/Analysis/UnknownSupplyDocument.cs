namespace SatisfactoryLedger.World.Contracts.Analysis;

public sealed record UnknownSupplyDocument(string Id, IReadOnlyDictionary<string, double> Delivered, double? StockMinutes, bool Confirmed);