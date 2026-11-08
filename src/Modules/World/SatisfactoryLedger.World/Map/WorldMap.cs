using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Map;

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
                    .Select(machine => new MapMachine(
                        machine.Id, machine.ClassName, machine.Recipe, machine.Position!, machine.Yaw ?? 0, machine.InputContents, machine.OutputContents))
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
                        conveyor.Curve!,
                        conveyor.Contents))
            ],
            [.. Having(BuildableDocumentRole.Logistics).Where(logistics => logistics.Curve is not { Count: > 0 }).Select(Building)],
            [.. Having(BuildableDocumentRole.Consumer).Select(Building)],
            snapshot.Footprints ?? []);
    }

    private static MapBuilding Building(BuildableDocument buildable) =>
        new(buildable.Id, buildable.ClassName, buildable.Position!, buildable.Yaw ?? 0, buildable.Contents);
}