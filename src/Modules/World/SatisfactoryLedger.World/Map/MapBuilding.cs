using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Map;

/// <summary>Another building on the map: a splitter, merger, lift, container or buffer, or a consumer (BR-W8).</summary>
/// <param name="Contents">What's on a lift or in a splitter's or merger's buffer at save time; null for the others.</param>
internal sealed record MapBuilding(string Id, string ClassName, PositionDocument Position, double Yaw, IReadOnlyDictionary<string, decimal>? Contents);