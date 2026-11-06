using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Map;

/// <summary>A machine on the map. How it runs comes from the findings, once the analysis is ready.</summary>
/// <param name="Yaw">Which way it faces, seen from above: degrees from the game's x axis towards its y axis.</param>
internal sealed record MapMachine(string Id, string ClassName, string? Recipe, PositionDocument Position, double Yaw);

/// <summary>A provider on the map (BR-W1): a miner or extractor, or a station that brings items in.</summary>
/// <param name="Resource">What it extracts: from its node, or its class (water); null for a station.</param>
/// <param name="Node">What it stands on, as the save names it: a resource node (as the Catalog names them, ADR-0015) or a water volume.</param>
/// <param name="Purity">The purity of that node: impure, normal or pure.</param>
internal sealed record MapProvider(string Id, string ClassName, string? Resource, string? Node, string? Purity, PositionDocument Position, double Yaw);

/// <summary>Another building on the map: a splitter, merger, lift, container or buffer, or a consumer (BR-W8).</summary>
internal sealed record MapBuilding(string Id, string ClassName, PositionDocument Position, double Yaw);

internal enum MapConveyorKind
{
    Belt,
    Pipe,
}

/// <summary>
/// A belt or pipe on the map, with its curve as built: a cubic Hermite spline through its points (see
/// <see cref="CurvePointDocument"/>). The map samples it into a line.
/// </summary>
internal sealed record MapConveyor(string Id, string ClassName, MapConveyorKind Kind, IReadOnlyList<CurvePointDocument> Curve);

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

/// <summary>
/// Everything of a snapshot the map draws, where it stands and which way it faces (ADR-0020). A snapshot imported
/// before version 5 has no curves, headings or footprints: its belts are missing and its buildings face along x.
/// </summary>
internal static class WorldMap
{
    public static WorldMapResponse For(SnapshotDocument snapshot)
    {
        Dictionary<string, ResourceNodeDocument> nodes = snapshot.ResourceNodes.ToDictionary(node => node.Name, StringComparer.Ordinal);
        List<BuildableDocument> placed = [.. snapshot.Buildables.Where(buildable => buildable.Position is not null)];
        IEnumerable<BuildableDocument> Having(BuildableDocumentRole role) => placed.Where(buildable => buildable.Role == role);

        return new WorldMapResponse(
            [
                .. Having(BuildableDocumentRole.Machine)
                    .Select(machine => new MapMachine(machine.Id, machine.ClassName, machine.Recipe, machine.Position!, machine.Yaw ?? 0))
            ],
            [
                .. Having(BuildableDocumentRole.Provider)
                    .Select(provider =>
                    {
                        ResourceNodeDocument? node = provider.Node is not null && nodes.TryGetValue(provider.Node, out ResourceNodeDocument? found) ? found : null;
                        return new MapProvider(
                            provider.Id, provider.ClassName, provider.Resource ?? node?.Resource, provider.Node, node?.Purity, provider.Position!, provider.Yaw ?? 0);
                    })
            ],
            [
                .. snapshot.Buildables.Where(buildable => buildable.Curve is { Count: > 0 })
                    .Select(conveyor => new MapConveyor(
                        conveyor.Id,
                        conveyor.ClassName,
                        conveyor.Kind == LogisticsKind.Pipe ? MapConveyorKind.Pipe : MapConveyorKind.Belt,
                        conveyor.Curve!))
            ],
            [.. Having(BuildableDocumentRole.Logistics).Where(logistics => logistics.Curve is not { Count: > 0 }).Select(Building)],
            [.. Having(BuildableDocumentRole.Consumer).Select(Building)],
            snapshot.Footprints ?? []);
    }

    private static MapBuilding Building(BuildableDocument buildable) => new(buildable.Id, buildable.ClassName, buildable.Position!, buildable.Yaw ?? 0);
}
