using SatisfactoryLedger.World.Analysis;
using SatisfactoryLedger.World.Contracts.Analysis;
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
        HashSet<ItemId> fluids = [.. snapshot.Fluids.Select(ItemId.Of)];
        Dictionary<string, BuildableDocument> buildables = snapshot.Buildables.ToDictionary(buildable => buildable.Id, StringComparer.Ordinal);

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

                FactoryMembers members = definition.TraceMembers(world, fluids);
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
                    Summaries(members.Outputs, buildables),
                    Summaries(members.Inputs, buildables),
                    missing);
            })
        ];
    }

    /// <summary>
    /// What the chosen machines would make a factory in this snapshot, before it's saved (part 2b(2)). Ids that aren't
    /// machines or generators here are left out; with none left, it owns and hands over nothing.
    /// </summary>
    /// <param name="analysis">The snapshot's stored analysis, for what crosses now; null while it isn't ready.</param>
    public static FactoryPreview Preview(SnapshotDocument snapshot, IEnumerable<string> machines, AnalysisDocument? analysis)
    {
        Dictionary<string, BuildableDocument> buildables = snapshot.Buildables.ToDictionary(buildable => buildable.Id, StringComparer.Ordinal);
        List<BuildableId> chosen = [.. machines
            .Distinct(StringComparer.Ordinal)
            .Where(id => buildables.TryGetValue(id, out BuildableDocument? buildable) && snapshot.IsMachineOrGenerator(buildable))
            .Select(BuildableId.Of)];
        if (chosen.Count == 0)
        {
            return new FactoryPreview([], [], [], [], []);
        }

        WorldGraph world = snapshot.ToWorldGraph();
        FactoryMembers members = new FactoryDefinition(chosen).TraceMembers(world, snapshot.Fluids.Select(ItemId.Of).ToHashSet());
        Dictionary<string, MachineStateDocument> states = (analysis?.Machines ?? []).ToDictionary(state => state.Id, StringComparer.Ordinal);
        return new FactoryPreview(
            [.. members.Logistics.Select(id => id.Id).Order(StringComparer.Ordinal)],
            Summaries(members.Outputs, buildables),
            Summaries(members.Inputs, buildables),
            PerItem(members.Outputs, (output, item) => FactoryOutputs.Over(output, world, analysis!).GetValueOrDefault(item.Id), analysis is not null),
            PerItem(members.Inputs, (input, item) => FactoryInputs.Arriving(input, item, world, analysis!, members, states), analysis is not null));
    }

    /// <summary>Per item over the connections, what crosses now: summed, most first; without rates when they aren't known.</summary>
    private static List<PreviewItem> PerItem(IEnumerable<FactoryConnection> connections, Func<FactoryConnection, ItemId, double> crossing, bool known)
    {
        Dictionary<string, double> perItem = new(StringComparer.Ordinal);
        foreach (FactoryConnection connection in connections)
        {
            foreach (ItemId item in connection.Items)
            {
                perItem[item.Id] = perItem.GetValueOrDefault(item.Id) + (known ? crossing(connection, item) : 0);
            }
        }

        return
        [
            .. perItem.OrderByDescending(entry => entry.Value).ThenBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => new PreviewItem(entry.Key, known ? SnapshotAnalysis.Round(entry.Value) : null)),
        ];
    }

    private static List<ConnectionSummary> Summaries(IEnumerable<FactoryConnection> connections, Dictionary<string, BuildableDocument> buildables)
    {
        BuildableReference Reference(BuildableId id) => new(id.Id, buildables[id.Id].ClassName);
        return
        [
            .. connections
                .Select(connection => new ConnectionSummary(
                    Reference(connection.Inside),
                    connection.Outside is { } outside ? Reference(outside) : null,
                    [.. connection.Items.Select(item => item.Id)]))
                .OrderBy(connection => connection.Inside.Id, StringComparer.Ordinal),
        ];
    }
}
