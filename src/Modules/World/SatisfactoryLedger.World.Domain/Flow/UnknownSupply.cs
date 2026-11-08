namespace SatisfactoryLedger.World.Domain.Flow;

/// <param name="StockMinutes">How long its current stock lasts at what it delivers; null when its stock doesn't hold what it delivers.</param>
/// <param name="Confirmed">True when the stock lasts at least 30 minutes (BR-A9).</param>
public sealed record UnknownSupply(BuildableId Source, IReadOnlyDictionary<ItemId, double> Delivered, double? StockMinutes, bool Confirmed);