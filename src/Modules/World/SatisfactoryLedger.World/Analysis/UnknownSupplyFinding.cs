using SatisfactoryLedger.World.Snapshots;
namespace SatisfactoryLedger.World.Analysis;

/// <summary>A source whose rate isn't known, and how long its stock lasts (BR-A9).</summary>
/// <param name="Delivered">Per item, what it delivers per minute.</param>
/// <param name="StockMinutes">How long its stock lasts at that rate; null when its stock doesn't hold what it delivers.</param>
/// <param name="Confirmed">True when the stock lasts at least 30 minutes; otherwise it's assumed supplied.</param>
internal sealed record UnknownSupplyFinding(
    string Id,
    string ClassName,
    IReadOnlyDictionary<string, double> Delivered,
    double? StockMinutes,
    bool Confirmed,
    PositionDocument? Position,
    bool Approximate);