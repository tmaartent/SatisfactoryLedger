using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.Contracts.Analysis;
using SatisfactoryLedger.World.Snapshots;
namespace SatisfactoryLedger.World.Analysis;

/// <summary>Turns a snapshot's stored analysis (ADR-0017) into findings a player can act on.</summary>
internal static class SnapshotAnalysis
{
    /// <summary>Results are meaningful to 0.0001 a minute, so that's what they show (BR-A10).</summary>
    private const int Decimals = 4;

    /// <summary>The findings of the whole world.</summary>
    public static AnalysisResponse For(SnapshotDocument snapshot, AnalysisDocument analysis) =>
        Findings(snapshot, analysis, analysis.UnsettledParts, _ => true, _ => true, _ => true, wholeWorld: true);

    /// <summary>
    /// The findings of one factory (BR-A14): its machines and logistics, and its suppliers. The simulation was of the
    /// whole world, because a factory depends on its supply. Null when the factory can't be traced in this snapshot
    /// (BR-W11).
    /// </summary>
    public static FactoryAnalysisResponse? ForFactory(SnapshotDocument snapshot, Factories.StoredFactory factory, AnalysisDocument analysis)
    {
        WorldGraph world = snapshot.ToWorldGraph();
        FactoryDefinition? definition = factory.ToDefinition(Factories.MachineMatch.In(snapshot, factory.Machines));
        if (definition is null || !definition.IsTraceableIn(world))
        {
            return null;
        }

        FactoryMembers members = definition.TraceMembers(world);
        bool IsMember(string id) => members.Contains(BuildableId.Of(id));
        List<UnsettledPartDocument> parts = [.. analysis.UnsettledParts.Where(part => part.Buildables.Any(IsMember))];
        AnalysisResponse findings = Findings(
            snapshot,
            analysis,
            parts,
            id => members.Machines.Contains(BuildableId.Of(id)),
            id => members.Logistics.Contains(BuildableId.Of(id)),
            id => members.Suppliers.Contains(BuildableId.Of(id)),
            // A generator or a drone port is never part of a factory (BR-W1, BR-W8).
            wholeWorld: false);

        return new FactoryAnalysisResponse(
            factory.Id,
            factory.Name,
            findings.Settled,
            findings.Summary,
            OutputOf(world, members, analysis),
            findings.Machines,
            findings.Bottlenecks,
            findings.UnusedSupply,
            findings.UnknownSupply);
    }

    /// <summary>Per item the producers make: what leaves over its outputs now, and what they'd make at 100%.</summary>
    private static List<FactoryOutput> OutputOf(WorldGraph world, FactoryMembers members, AnalysisDocument analysis)
    {
        Dictionary<string, double> now = new(StringComparer.Ordinal);
        foreach (FactoryConnection output in members.Outputs)
        {
            IReadOnlyDictionary<string, double> leaving = Over(output, world, analysis);
            foreach (string item in output.Items.Select(item => item.Id))
            {
                now[item] = now.GetValueOrDefault(item) + leaving.GetValueOrDefault(item);
            }
        }

        Dictionary<string, double> atFullSpeed = new(StringComparer.Ordinal);
        foreach (Machine producer in members.Producers.Select(id => world.Find(id)).OfType<Machine>())
        {
            foreach ((ItemId item, SharedKernel.Rate rate) in producer.Production)
            {
                atFullSpeed[item.Id] = atFullSpeed.GetValueOrDefault(item.Id) + rate.ToDouble();
            }
        }

        return
        [
            .. atFullSpeed.Keys.Union(now.Keys).Order(StringComparer.Ordinal)
                .Select(item => new FactoryOutput(item, Round(now.GetValueOrDefault(item)), Round(atFullSpeed.GetValueOrDefault(item))))
        ];
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

    /// <summary>
    /// The findings of the machines, conduits and suppliers the filters let through; worst first. Whether they settled
    /// is read from <paramref name="unsettled"/>: the parts of the world they're in that didn't settle.
    /// </summary>
    /// <param name="wholeWorld">True for the whole world: generators and drone ports are listed too.</param>
    private static AnalysisResponse Findings(
        SnapshotDocument snapshot,
        AnalysisDocument analysis,
        IReadOnlyCollection<UnsettledPartDocument> unsettled,
        Func<string, bool> isMachine,
        Func<string, bool> isConduit,
        Func<string, bool> isSupplier,
        bool wholeWorld)
    {
        Dictionary<string, BuildableDocument> buildables = snapshot.Buildables.ToDictionary(buildable => buildable.Id, StringComparer.Ordinal);
        HashSet<string> approximate = analysis.Approximate();
        WorldGraph world = snapshot.ToWorldGraph();
        (string? Item, double? Minutes) Stock(MachineStateDocument state) => OnStock(world, snapshot, analysis, state);

        // The flow simulates generators like machines (BR-A8), but they're consumers: their own findings (BR-A15).
        List<MachineStateDocument> machines = [.. analysis.Machines
            .Where(machine => buildables[machine.Id].Role == BuildableDocumentRole.Machine && isMachine(machine.Id))];
        List<MachineStateDocument> generators = wholeWorld
            ? [.. analysis.Machines.Where(machine => buildables[machine.Id].Role == BuildableDocumentRole.Consumer && buildables[machine.Id].Fuel is not null)]
            : [];
        return new AnalysisResponse(
            unsettled.Count == 0,
            new AnalysisSummary(
                machines.Count,
                machines.Count(machine => machine.Limit == MachineLimit.None),
                machines.Count(machine => machine.Limit == MachineLimit.Starved),
                machines.Count(machine => machine.Limit == MachineLimit.OutputBlocked),
                machines.Count(machine => machine.Limit == MachineLimit.NoRecipe)),
            [
                .. unsettled.OrderByDescending(part => part.Buildables.Count)
                    .Select(part => new ApproximatePart(part.Buildables.Count, part.Sweeps, part.Buildables[0]))
            ],
            [
                .. machines
                    .OrderBy(machine => machine.Utilisation).ThenBy(machine => machine.Id, StringComparer.Ordinal)
                    .Select(machine => new MachineFinding(
                        machine.Id,
                        buildables[machine.Id].ClassName,
                        buildables[machine.Id].Recipe,
                        Round(machine.Utilisation),
                        machine.Limit,
                        machine.LimitingItem,
                        buildables[machine.Id].Position,
                        approximate.Contains(machine.Id),
                        Shares(machine.StarvedOf),
                        Shares(machine.BlockedOn),
                        Stock(machine).Item,
                        Stock(machine).Minutes))
            ],
            [
                .. analysis.Bottlenecks.Where(bottleneck => isConduit(bottleneck.Id))
                    .OrderByDescending(bottleneck => bottleneck.OfferedMore).ThenBy(bottleneck => bottleneck.Id, StringComparer.Ordinal)
                    .Select(bottleneck => new BottleneckFinding(
                        bottleneck.Id,
                        buildables[bottleneck.Id].ClassName,
                        Round(bottleneck.Capacity),
                        Round(bottleneck.Flow),
                        Round(bottleneck.OfferedMore),
                        buildables[bottleneck.Id].Position,
                        approximate.Contains(bottleneck.Id)))
            ],
            [
                .. analysis.UnusedSupply.Where(supply => isSupplier(supply.Id))
                    .OrderByDescending(supply => supply.Offered - supply.Delivered).ThenBy(supply => supply.Id, StringComparer.Ordinal)
                    .Select(supply => new UnusedSupplyFinding(
                        supply.Id,
                        buildables[supply.Id].ClassName,
                        supply.Item,
                        Round(supply.Offered),
                        Round(supply.Delivered),
                        buildables[supply.Id].Position,
                        approximate.Contains(supply.Id)))
            ],
            [
                // Assumed supplied first, the shortest stock first: those need checking in the game.
                .. analysis.UnknownSupply.Where(supply => isSupplier(supply.Id))
                    .OrderBy(supply => supply.Confirmed).ThenBy(supply => supply.StockMinutes ?? -1).ThenBy(supply => supply.Id, StringComparer.Ordinal)
                    .Select(supply => new UnknownSupplyFinding(
                        supply.Id,
                        buildables[supply.Id].ClassName,
                        supply.Delivered.OrderBy(entry => entry.Key, StringComparer.Ordinal).ToDictionary(entry => entry.Key, entry => Round(entry.Value)),
                        supply.StockMinutes is { } minutes ? Round(minutes) : null,
                        supply.Confirmed,
                        buildables[supply.Id].Position,
                        approximate.Contains(supply.Id)))
            ],
            [
                .. generators
                    .OrderBy(generator => generator.Utilisation).ThenBy(generator => generator.Id, StringComparer.Ordinal)
                    .Select(generator => new GeneratorFinding(
                        generator.Id,
                        buildables[generator.Id].ClassName,
                        buildables[generator.Id].Fuel!,
                        Round(generator.Utilisation),
                        generator.Limit,
                        generator.LimitingItem,
                        buildables[generator.Id].Position,
                        approximate.Contains(generator.Id),
                        Shares(generator.StarvedOf),
                        Shares(generator.BlockedOn),
                        Stock(generator).Item,
                        Stock(generator).Minutes))
            ],
            wholeWorld ? DronePorts(world, snapshot, analysis, buildables, approximate) : []);
    }

    /// <summary>What reaches a buildable of an item per minute: what flows into the belts and pipes that end at it.</summary>
    private static double Supplied(SnapshotDocument snapshot, AnalysisDocument analysis, string id, string item) =>
        snapshot.Connections.Where(connection => connection.To == id)
            .Sum(connection => analysis.Into.GetValueOrDefault(connection.From)?.GetValueOrDefault(item) ?? 0);

    /// <summary>
    /// BR-A17: the stocked input a machine or generator runs on, because its belts bring less than it uses, and how long
    /// that stock lasts. When several do, the one that runs out first.
    /// </summary>
    private static (string? Item, double? Minutes) OnStock(WorldGraph world, SnapshotDocument snapshot, AnalysisDocument analysis, MachineStateDocument state)
    {
        (IReadOnlyDictionary<ItemId, SharedKernel.Rate> demand, IReadOnlyDictionary<ItemId, decimal> stock) = world.Find(BuildableId.Of(state.Id)) switch
        {
            Machine machine => (machine.Demand, machine.InputStock),
            Consumer { Generation: { } generation } generator =>
                ((IReadOnlyDictionary<ItemId, SharedKernel.Rate>)new Dictionary<ItemId, SharedKernel.Rate> { [generation.Fuel] = generation.Burns }, generator.InputStock),
            _ => (new Dictionary<ItemId, SharedKernel.Rate>(), new Dictionary<ItemId, decimal>()),
        };

        (string? Item, double? Minutes) first = (null, null);
        foreach ((ItemId item, SharedKernel.Rate rate) in demand)
        {
            double perMinute = rate.ToDouble();
            if (!stock.TryGetValue(item, out decimal held) || perMinute <= 0 || (double)held < perMinute * SteadyState.ConfirmedMinutes)
            {
                continue;
            }

            double shortfall = perMinute * state.Utilisation - Supplied(snapshot, analysis, state.Id, item.Id);
            if (shortfall > SteadyState.Tolerance && (first.Minutes is null || (double)held / shortfall < first.Minutes))
            {
                first = (item.Id, Round((double)held / shortfall));
            }
        }

        return first;
    }

    /// <summary>BR-A16: every drone port where drones refuel at a known rate, the least supplied first.</summary>
    private static List<DronePortFinding> DronePorts(
        WorldGraph world, SnapshotDocument snapshot, AnalysisDocument analysis, Dictionary<string, BuildableDocument> buildables, HashSet<string> approximate)
    {
        List<(string Id, string Fuel, double Burns)> ports = [];
        foreach (Buildable buildable in world.Buildables)
        {
            (IReadOnlySet<ItemId> burns, SharedKernel.Rate? rate) = buildable switch
            {
                Provider provider => (provider.Burns, provider.BurnRate),
                Consumer consumer => (consumer.Burns, consumer.BurnRate),
                _ => (new HashSet<ItemId>(), null),
            };
            if (rate is not null)
            {
                ports.Add((buildable.Id.Id, burns.First().Id, rate.ToDouble()));
            }
        }

        return
        [
            .. ports
                .Select(port => (port.Id, port.Fuel, port.Burns, Supplied: Supplied(snapshot, analysis, port.Id, port.Fuel)))
                .OrderBy(port => port.Supplied / port.Burns).ThenBy(port => port.Id, StringComparer.Ordinal)
                .Select(port => new DronePortFinding(
                    port.Id,
                    buildables[port.Id].ClassName,
                    port.Fuel,
                    Round(port.Burns),
                    Round(port.Supplied),
                    buildables[port.Id].Position,
                    approximate.Contains(port.Id)))
        ];
    }

    /// <summary>Shares worst first, to 4 decimals; none when the analysis is older than version 6.</summary>
    private static Dictionary<string, double> Shares(IReadOnlyDictionary<string, double>? shares)
    {
        Dictionary<string, double> ordered = new(StringComparer.Ordinal);
        foreach ((string item, double share) in (shares ?? new Dictionary<string, double>()).OrderBy(entry => entry.Value).ThenBy(entry => entry.Key, StringComparer.Ordinal))
        {
            ordered[item] = Round(share);
        }

        return ordered;
    }

    private static double Round(double value) => Math.Round(value, Decimals, MidpointRounding.AwayFromZero);
}