using SatisfactoryLedger.World.Snapshots;
namespace SatisfactoryLedger.World.Analysis;

/// <summary>A provider delivering less than it offers (BR-A13). Rates per minute.</summary>
internal sealed record UnusedSupplyFinding(
    string Id,
    string ClassName,
    string Item,
    double Offered,
    double Delivered,
    PositionDocument? Position,
    bool Approximate);