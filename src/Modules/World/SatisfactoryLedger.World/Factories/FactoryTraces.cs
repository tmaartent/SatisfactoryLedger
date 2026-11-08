using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Factories;

/// <summary>Factory definitions traced in a stored snapshot.</summary>
internal static class FactoryTraces
{
    /// <summary>Builds the snapshot's graph once and traces every factory in it.</summary>
    public static IReadOnlyList<FactoryTrace> TraceAll(SnapshotDocument snapshot, IEnumerable<StoredFactory> factories)
    {
        WorldGraph world = snapshot.ToWorldGraph();
        Dictionary<string, BuildableDocument> buildables = snapshot.Buildables.ToDictionary(buildable => buildable.Id, StringComparer.Ordinal);

        return
        [
            .. factories.Select(factory =>
            {
                FactoryDefinition definition = factory.ToDefinition();
                if (!definition.IsTraceableIn(world))
                {
                    return new FactoryTrace(factory.Id, factory.Name, false, [], 0);
                }

                FactoryMembers members = definition.TraceMembers(world);
                return new FactoryTrace(
                    factory.Id,
                    factory.Name,
                    true,
                    [
                        .. members.Machines
                            .Select(id => buildables[id.Id])
                            .Select(machine => new MachineSummary(machine.Id, machine.ClassName, machine.Recipe))
                            .OrderBy(machine => machine.Id, StringComparer.Ordinal)
                    ],
                    members.Logistics.Count);
            })
        ];
    }
}