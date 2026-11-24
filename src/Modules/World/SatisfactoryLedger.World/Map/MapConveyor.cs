using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Map;

/// <summary>
/// A belt or pipe on the map, with its curve as built: a cubic Hermite spline through its points (see
/// <see cref="CurvePointDocument"/>). The map samples it into a line.
/// </summary>
/// <param name="Contents">What's on it at save time, per item, fluids in m³; null in an older snapshot (for a pipe before
/// version 15).</param>
/// <param name="LooseEnds">How many of its ends are connected to nothing: the 3D view draws it red (asked 2026-11-23). 0 in
/// a snapshot from before they were read.</param>
/// <param name="HeadLiftToSpare">For a pipe some fluid output feeds, the metres of head lift it has to spare, estimated
/// (BR-A22): negative when it climbs above the head lift. Null for a belt, and for a pipe nothing feeds.</param>
internal sealed record MapConveyor(
    string Id,
    string ClassName,
    MapConveyorKind Kind,
    IReadOnlyList<CurvePointDocument> Curve,
    IReadOnlyDictionary<string, decimal>? Contents,
    int LooseEnds,
    double? HeadLiftToSpare = null);