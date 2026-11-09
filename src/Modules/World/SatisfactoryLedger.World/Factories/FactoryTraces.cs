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
        BuildableReference Reference(BuildableId id) => new(id.Id, buildables[id.Id].ClassName);
        ConnectionSummary Summary(FactoryConnection connection) => new(
            Reference(connection.Inside),
            connection.Outside is { } outside ? Reference(outside) : null,
            [.. connection.Items.Select(item => item.Id)]);

        return
        [
            .. factories.Select(factory =>
            {
                MachineMatch match = MachineMatch.In(snapshot, factory.Machines);
                List<BuildableReference> missing = [.. match.Missing.Select(machine => new BuildableReference(machine.Id, machine.ClassName))];
                FactoryDefinition? definition = factory.ToDefinition(match);
                if (definition is null || !definition.IsTraceableIn(world))
                {
                    return new FactoryTrace(factory.Id, factory.Name, false, [], 0, [], [], missing);
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
                    members.Logistics.Count,
                    [.. members.Outputs.Select(Summary).OrderBy(output => output.Inside.Id, StringComparer.Ordinal)],
                    [.. members.Inputs.Select(Summary).OrderBy(input => input.Inside.Id, StringComparer.Ordinal)],
                    missing);
            })
        ];
    }
}
