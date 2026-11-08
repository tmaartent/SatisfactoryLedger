using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Map;

/// <summary>A machine on the map. How it runs comes from the findings, once the analysis is ready.</summary>
/// <param name="Yaw">Which way it faces, seen from above: degrees from the game's x axis towards its y axis.</param>
/// <param name="InputContents">The ingredients in it at save time, per item (fluids in m³); null in older snapshots.</param>
/// <param name="OutputContents">The products waiting in it to leave at save time.</param>
internal sealed record MapMachine(
    string Id,
    string ClassName,
    string? Recipe,
    PositionDocument Position,
    double Yaw,
    IReadOnlyDictionary<string, decimal>? InputContents,
    IReadOnlyDictionary<string, decimal>? OutputContents);