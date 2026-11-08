using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Map;

/// <summary>A provider on the map (BR-W1): a miner or extractor, or a station that brings items in.</summary>
/// <param name="Resource">What it extracts: from its node, or its class (water); null for a station.</param>
/// <param name="Node">What it stands on, as the save names it: a resource node (as the Catalog names them, ADR-0015) or a water volume.</param>
/// <param name="Purity">The purity of that node: impure, normal or pure.</param>
internal sealed record MapProvider(string Id, string ClassName, string? Resource, string? Node, string? Purity, PositionDocument Position, double Yaw);