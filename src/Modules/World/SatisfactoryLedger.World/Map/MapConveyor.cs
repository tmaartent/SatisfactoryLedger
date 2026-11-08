using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Map;

/// <summary>
/// A belt or pipe on the map, with its curve as built: a cubic Hermite spline through its points (see
/// <see cref="CurvePointDocument"/>). The map samples it into a line.
/// </summary>
/// <param name="Contents">What's on it at save time, per item; null for a pipe or an older snapshot.</param>
internal sealed record MapConveyor(
    string Id, string ClassName, MapConveyorKind Kind, IReadOnlyList<CurvePointDocument> Curve, IReadOnlyDictionary<string, decimal>? Contents);