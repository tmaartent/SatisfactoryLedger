using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Map;

/// <summary>Another building on the map: a splitter, merger, lift, container or buffer, or a consumer (BR-W8).</summary>
/// <param name="Contents">What's on a lift or in a splitter's or merger's buffer at save time; null for the others.</param>
/// <param name="IsGenerator">A power generator: a factory can have it as one of its machines (BR-W2).</param>
internal sealed record MapBuilding(
    string Id, string ClassName, PositionDocument Position, double Yaw, IReadOnlyDictionary<string, decimal>? Contents, bool IsGenerator);