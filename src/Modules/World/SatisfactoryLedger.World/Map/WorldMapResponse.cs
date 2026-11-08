using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Map;

/// <summary>What the world map draws of a snapshot. Positions in the game's units (centimetres).</summary>
/// <param name="Logistics">Splitters, mergers, lifts, containers and the like: logistics without a curve.</param>
/// <param name="Footprints">The ground each building class covers, by class name: the map turns it with each building.</param>
internal sealed record WorldMapResponse(
    IReadOnlyList<MapMachine> Machines,
    IReadOnlyList<MapProvider> Providers,
    IReadOnlyList<MapConveyor> Conveyors,
    IReadOnlyList<MapBuilding> Logistics,
    IReadOnlyList<MapBuilding> Consumers,
    IReadOnlyList<FootprintDocument> Footprints);