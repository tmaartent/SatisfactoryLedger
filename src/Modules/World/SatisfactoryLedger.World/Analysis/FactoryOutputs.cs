using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Contracts.Analysis;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Factories;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Analysis;

/// <summary>
/// What a factory makes per item (BR-A14): now and at 100%, where it goes, what its receivers would take, what's sunk,
/// and what can't leave (BR-A18).
/// </summary>
internal static class FactoryOutputs
{
    /// <summary>The class of the AWESOME Sink: what reaches it is sunk.</summary>
    private const string SinkClass = "Build_ResourceSink_C";

    /// <summary>The consumers that use up what they get, shown by building (decided 2026-11-11). Stations and uploaders store it.</summary>
    private static readonly HashSet<string> UsesUp = new(["Build_Portal_C", "Build_AlienPowerBuilding_C"], StringComparer.Ordinal);

    /// <param name="factories">The world's factories, this one among them: who a receiving machine belongs to.</param>
    public static List<FactoryOutput> Of(
        SnapshotDocument snapshot,
        WorldGraph world,
        StoredFactory factory,
        FactoryMembers members,
        AnalysisDocument analysis,
        IEnumerable<StoredFactory> factories)
    {
        Dictionary<string, MachineStateDocument> states = analysis.Machines.ToDictionary(state => state.Id, StringComparer.Ordinal);
        Dictionary<string, BuildableDocument> buildables = snapshot.Buildables.ToDictionary(buildable => buildable.Id, StringComparer.Ordinal);
        Dictionary<BuildableId, StoredFactory> owners = OwnersIn(snapshot, factory, members, factories);

        Dictionary<string, double> now = new(StringComparer.Ordinal);
        Dictionary<string, Dictionary<(OutputDestinationKind Kind, StoredFactory? Factory, string? Building), double>> destinations = new(StringComparer.Ordinal);
        Dictionary<string, List<BuildableId>> startsOf = new(StringComparer.Ordinal);
        foreach (FactoryConnection output in members.Outputs)
        {
            IReadOnlyDictionary<string, double> leaving = Over(output, world, analysis);
            BuildableId start = output.Outside ?? output.Inside;
            foreach (ItemId item in output.Items)
            {
                double amount = leaving.GetValueOrDefault(item.Id);
                now[item.Id] = now.GetValueOrDefault(item.Id) + amount;
                startsOf.TryAdd(item.Id, []);
                startsOf[item.Id].Add(start);

                Dictionary<(OutputDestinationKind, StoredFactory?, string?), double> to = destinations.TryGetValue(item.Id, out var found) ? found : destinations[item.Id] = [];
                foreach ((BuildableId end, double perMinute) in OutputDestinations.Of(world, start, item, amount, id => FlowInto(id, item)))
                {
                    var key = Destination(end);
                    to[key] = to.GetValueOrDefault(key) + perMinute;
                }
            }
        }

        // Only what leaves: a product its own machines take all of, such as gas unpacked for its blenders, isn't output.
        HashSet<ItemId> leaves = [.. members.Outputs.SelectMany(output => output.Items)];

        // At 100%, what its machines make of it less what its own machines take of it (decided 2026-11-11): a loop
        // that makes rubber and takes some back for its plastic only lets the rest leave.
        Dictionary<string, double> atFullSpeed = new(StringComparer.Ordinal);
        foreach (Buildable member in members.Machines.Select(id => world.Find(id)).OfType<Buildable>())
        {
            foreach ((ItemId item, Rate rate) in Makes(member).Where(product => leaves.Contains(product.Key)))
            {
                atFullSpeed[item.Id] = atFullSpeed.GetValueOrDefault(item.Id) + rate.ToDouble();
            }

            foreach ((ItemId item, Rate rate) in Takes(member).Where(input => leaves.Contains(input.Key)))
            {
                atFullSpeed[item.Id] = atFullSpeed.GetValueOrDefault(item.Id) - rate.ToDouble();
            }
        }

        // What an output blocked producer would make at 100% but can't get out.
        Dictionary<string, double> cannotLeave = new(StringComparer.Ordinal);
        foreach (Buildable producer in members.Producers.Select(id => world.Find(id)).OfType<Buildable>())
        {
            if (states.GetValueOrDefault(producer.Id.Id) is not { Limit: MachineLimit.OutputBlocked } state)
            {
                continue;
            }

            foreach ((ItemId item, Rate rate) in Makes(producer).Where(product => leaves.Contains(product.Key)))
            {
                cannotLeave[item.Id] = cannotLeave.GetValueOrDefault(item.Id) + rate.ToDouble() * (1 - state.Utilisation);
            }
        }

        return
        [
            .. atFullSpeed.Keys.Union(now.Keys).Order(StringComparer.Ordinal).Select(item =>
            {
                Dictionary<(OutputDestinationKind Kind, StoredFactory? Factory, string? Building), double> to = destinations.GetValueOrDefault(item) ?? [];
                List<BuildableId> starts = startsOf.GetValueOrDefault(item) ?? [];
                ItemId id = ItemId.Of(item);
                double need = OutputDestinations.ReceiversNeed(world, starts, id).ToDouble();
                double get = OutputDestinations.ReceiversGet(world, starts, id, receiver => FlowInto(receiver, id));

                // What this factory brings its receivers: what reaches a machine or generator, of a factory or none.
                double brings = to.Where(entry => entry.Key.Kind is OutputDestinationKind.Factory or OutputDestinationKind.NoFactory).Sum(entry => entry.Value);
                return new FactoryOutput(
                    item,
                    SnapshotAnalysis.Round(now.GetValueOrDefault(item)),
                    SnapshotAnalysis.Round(Math.Max(0, atFullSpeed.GetValueOrDefault(item))),
                    SnapshotAnalysis.Round(need),
                    SnapshotAnalysis.Round(Math.Max(0, get - brings)),
                    SnapshotAnalysis.Round(Math.Max(0, need - get)),
                    SnapshotAnalysis.Round(to.Where(entry => entry.Key.Kind == OutputDestinationKind.Sink).Sum(entry => entry.Value)),
                    SnapshotAnalysis.Round(cannotLeave.GetValueOrDefault(item)),
                    [
                        .. to.Where(entry => SnapshotAnalysis.Round(entry.Value) > 0)
                            .OrderByDescending(entry => entry.Value)
                            .Select(entry => new OutputDestination(entry.Key.Kind, entry.Key.Factory?.Id, entry.Key.Factory?.Name, entry.Key.Building, SnapshotAnalysis.Round(entry.Value)))
                    ]);
            })
        ];

        // What flows into a buildable, of one item: the stored flow into a belt, pipe or container; for a machine or a
        // generator, which the analysis doesn't keep flows into, what it takes at the rate it runs.
        double FlowInto(BuildableId id, ItemId item)
        {
            if (analysis.Into.TryGetValue(id.Id, out IReadOnlyDictionary<string, double>? flows))
            {
                return flows.GetValueOrDefault(item.Id);
            }

            double utilisation = states.GetValueOrDefault(id.Id)?.Utilisation ?? 0;
            return utilisation * (world.Find(id) switch
            {
                Machine machine => machine.Demand.GetValueOrDefault(item)?.ToDouble() ?? 0,
                Consumer { Generation: { } generation } when generation.Fuel == item => generation.Burns.ToDouble(),
                Consumer { Generation: { } generation } when generation.Supplemental == item => generation.SupplementalNeeded.ToDouble(),
                _ => 0,
            });
        }

        (OutputDestinationKind, StoredFactory?, string?) Destination(BuildableId end) => world.Find(end) switch
        {
            Machine or Consumer { IsGenerator: true } when owners.TryGetValue(end, out StoredFactory? owner) => (OutputDestinationKind.Factory, owner, null),
            Machine or Consumer { IsGenerator: true } => (OutputDestinationKind.NoFactory, null, null),
            Consumer when ClassOf(end) == SinkClass => (OutputDestinationKind.Sink, null, null),

            Consumer when ClassOf(end) is { } building && UsesUp.Contains(building) => (OutputDestinationKind.Consumer, null, building),
            Logistics { Kind: LogisticsKind.Belt or LogisticsKind.Lift or LogisticsKind.Pipe or LogisticsKind.PipeNetwork } => (OutputDestinationKind.Nowhere, null, null),
            _ => (OutputDestinationKind.Storage, null, null),
        };

        string? ClassOf(BuildableId id) => buildables.GetValueOrDefault(id.Id)?.ClassName;
    }

    /// <summary>What one of its machines makes at 100%: a machine its recipe's products, a generator its waste (BR-A8).</summary>
    private static IEnumerable<KeyValuePair<ItemId, Rate>> Makes(Buildable member) => member switch
    {
        Machine machine => machine.Production,
        Consumer { Generation: { Byproduct: { } waste } generation } => [new(waste, generation.Leaves)],
        _ => [],
    };

    /// <summary>What one of its machines takes at 100%: a machine its recipe's inputs, a generator its fuel and water.</summary>
    private static IEnumerable<KeyValuePair<ItemId, Rate>> Takes(Buildable member) => member switch
    {
        Machine machine => machine.Demand,
        Consumer { Generation: { Supplemental: { } water } generation } => [new(generation.Fuel, generation.Burns), new(water, generation.SupplementalNeeded)],
        Consumer { Generation: { } generation } => [new(generation.Fuel, generation.Burns)],
        _ => [],
    };

    /// <summary>
    /// Who each machine in this snapshot belongs to: this factory's own members, and the other factories' machines as
    /// matched in it (BR-W11). The first factory wins for a machine stored in two, from before BR-W5.
    /// </summary>
    private static Dictionary<BuildableId, StoredFactory> OwnersIn(
        SnapshotDocument snapshot, StoredFactory factory, FactoryMembers members, IEnumerable<StoredFactory> factories)
    {
        Dictionary<BuildableId, StoredFactory> owners = members.Machines.ToDictionary(id => id, _ => factory);
        foreach (StoredFactory other in factories.Where(other => other.Id != factory.Id))
        {
            foreach (BuildableId id in MachineMatch.In(snapshot, other.Machines).Ids)
            {
                owners.TryAdd(id, other);
            }
        }

        return owners;
    }

    /// <summary>
    /// What flows over an output, per item and minute. The stored analysis keeps what flows into each belt, lift, pipe
    /// and container in a line (ADR-0017), so it's read from there:
    /// <list type="bullet">
    /// <item>into a storage container at the end of the line: what flows into the logistics that feed it;</item>
    /// <item>from logistics with one way out: what flows into it;</item>
    /// <item>otherwise into the logistics across. When that's fed from elsewhere too, such as a pipe network shared
    /// with other factories, this counts their part as well.</item>
    /// </list>
    /// </summary>
    private static IReadOnlyDictionary<string, double> Over(FactoryConnection output, WorldGraph world, AnalysisDocument analysis)
    {
        IReadOnlyDictionary<string, double> Into(BuildableId id) => analysis.Into.GetValueOrDefault(id.Id) ?? new Dictionary<string, double>();

        if (output.Outside is null)
        {
            return world.Upstream(output.Inside)
                .SelectMany(feeder => Into(feeder.Id))
                .GroupBy(flow => flow.Key, StringComparer.Ordinal)
                .ToDictionary(item => item.Key, item => item.Sum(flow => flow.Value), StringComparer.Ordinal);
        }

        if (world.Find(output.Inside) is Logistics && world.Downstream(output.Inside).Count() == 1)
        {
            return Into(output.Inside);
        }

        return world.Find(output.Outside) is Logistics ? Into(output.Outside) : new Dictionary<string, double>();
    }
}
