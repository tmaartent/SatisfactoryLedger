using SatisfactoryLedger.World.Snapshots;
namespace SatisfactoryLedger.World.Analysis;

/// <summary>A belt, lift or pipe at its capacity while more is offered upstream (BR-A12). Rates per minute.</summary>
internal sealed record BottleneckFinding(
    string Id,
    string ClassName,
    double Capacity,
    double Flow,
    double OfferedMore,
    PositionDocument? Position,
    bool Approximate);