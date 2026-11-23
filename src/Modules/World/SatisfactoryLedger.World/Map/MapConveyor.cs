using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Map;

/// <summary>
/// A belt or pipe on the map, with its curve as built: a cubic Hermite spline through its points (see
/// <see cref="CurvePointDocument"/>). The map samples it into a line.
/// </summary>
/// <param name="Contents">What's on it at save time, per item; null for a pipe or an older snapshot.</param>
/// <param name="LooseEnds">How many of its ends are connected to nothing: the 3D view draws it red (asked 2026-11-23). 0 in
/// a snapshot from before they were read.</param>
internal sealed record MapConveyor(
    string Id,
    string ClassName,
    MapConveyorKind Kind,
    IReadOnlyList<CurvePointDocument> Curve,
    IReadOnlyDictionary<string, decimal>? Contents,
    int LooseEnds);