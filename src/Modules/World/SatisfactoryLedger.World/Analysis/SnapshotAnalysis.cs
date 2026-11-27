using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.Domain.Tracing;
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
    /// <param name="factories">The world's factories: where its output goes (BR-A18).</param>
    public static FactoryAnalysisResponse? ForFactory(
        SnapshotDocument snapshot, Factories.StoredFactory factory, AnalysisDocument analysis, IEnumerable<Factories.StoredFactory> factories)
    {
        WorldGraph world = snapshot.ToWorldGraph();
        FactoryDefinition? definition = factory.ToDefinition(Factories.MachineMatch.In(snapshot, factory.Machines));
        if (definition is null || !definition.IsTraceableIn(world))
        {
            return null;
        }

        FactoryMembers members = definition.TraceMembers(world, snapshot.Fluids.Select(ItemId.Of).ToHashSet());
        bool IsMember(string id) => members.Contains(BuildableId.Of(id));
        List<UnsettledPartDocument> parts = [.. analysis.UnsettledParts.Where(part => part.Buildables.Any(IsMember))];
        AnalysisResponse findings = Findings(
            snapshot,
            analysis,
            parts,
            id => members.Machines.Contains(BuildableId.Of(id)),
            id => members.Logistics.Contains(BuildableId.Of(id)),
            id => members.Suppliers.Contains(BuildableId.Of(id)),
            // A drone port is never part of a factory (BR-W1, BR-W8); a generator is, when chosen as one of its machines.
            wholeWorld: false);
        FlowReadings readings = AnalysisReadings.Of(world, analysis);
        FactoryHandovers handovers = SnapshotHandovers.Of(snapshot, world, factory, members, readings, factories);
        Dictionary<Guid, string> names = SnapshotHandovers.NamesOf(factory, factories);

        return new FactoryAnalysisResponse(
            factory.Id,
            factory.Name,
            findings.Settled,
            findings.Summary,
            findings.GeneratorSummary,
            FactoryOutputs.Of(handovers, names),
            FactoryInputs.Of(handovers, names),
            PowerOf(snapshot, members, readings),
            findings.Machines,
            findings.Generators,
            findings.Bottlenecks,
            findings.UnusedSupply,
            findings.UnknownSupply);
    }

    /// <summary>
    /// The power its generators make (decided 2026-11-11): at 100%, each generator's power at its clock speed; now, that
    /// times how fast the flow lets it run. A generator whose fuel isn't known isn't simulated: it counts at 100% only.
    /// Null for a factory without generators. Also for the preview while choosing a factory's machines (asked 2026-11-24).
    /// </summary>
    internal static FactoryPower? PowerOf(SnapshotDocument snapshot, FactoryMembers members, FlowReadings readings)
    {
        Dictionary<string, GeneratorDocument> catalog = (snapshot.Generators ?? []).ToDictionary(generator => generator.Id, StringComparer.Ordinal);
        List<(double AtFullSpeed, double Utilisation)> generators = [.. snapshot.Buildables
            .Where(buildable => members.Machines.Contains(BuildableId.Of(buildable.Id)) && buildable.Role == BuildableDocumentRole.Consumer)
            .Where(buildable => catalog.ContainsKey(buildable.ClassName))
            .Select(buildable => (
                (double)(catalog[buildable.ClassName].PowerMegawatts * (buildable.ClockPercent ?? 100) / 100),
                readings.Machine(BuildableId.Of(buildable.Id))?.Utilisation ?? 0))];
        return generators.Count == 0
            ? null
            : new FactoryPower(
                Round(generators.Sum(generator => generator.AtFullSpeed * generator.Utilisation)),
                Round(generators.Sum(generator => generator.AtFullSpeed)));
    }

    /// <summary>
    /// The findings of the machines, conduits and suppliers the filters let through; worst first. Whether they settled
    /// is read from <paramref name="unsettled"/>: the parts of the world they're in that didn't settle.
    /// </summary>
    /// <param name="wholeWorld">True for the whole world: drone ports are listed too.</param>
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
        FlowReadings readings = AnalysisReadings.Of(world, analysis);
        (string? Item, double? Minutes) Stock(MachineStateDocument state) => OnStock(world, readings, state);

        // The flow simulates generators like machines (BR-A8), but they're consumers: their own findings (BR-A15).
        List<MachineStateDocument> machines = [.. analysis.Machines
            .Where(machine => buildables[machine.Id].Role == BuildableDocumentRole.Machine && isMachine(machine.Id))];
        // A factory lists the generators it has as machines (BR-W2); the whole world lists them all.
        List<MachineStateDocument> generators = [.. analysis.Machines
            .Where(machine => buildables[machine.Id].Role == BuildableDocumentRole.Consumer && buildables[machine.Id].Fuel is not null && isMachine(machine.Id))];
        // A generator whose fuel isn't known isn't simulated (BR-A15); the summary still counts it.
        int fuelUnknown = snapshot.Buildables.Count(buildable =>
            buildable.Role == BuildableDocumentRole.Consumer && buildable.Fuel is null && snapshot.IsMachineOrGenerator(buildable) && isMachine(buildable.Id));
        return new AnalysisResponse(
            unsettled.Count == 0,
            new AnalysisSummary(
                machines.Count,
                machines.Count(machine => machine.Limit == MachineLimit.None),
                machines.Count(machine => machine.Limit == MachineLimit.Starved),
                machines.Count(machine => machine.Limit == MachineLimit.OutputBlocked),
                machines.Count(machine => machine.Limit == MachineLimit.NoRecipe)),
            new GeneratorSummary(
                generators.Count + fuelUnknown,
                generators.Count(generator => generator.Limit == MachineLimit.None),
                generators.Count(generator => generator.Limit == MachineLimit.Starved),
                generators.Count(generator => generator.Limit == MachineLimit.OutputBlocked),
                fuelUnknown),
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
            wholeWorld ? DronePorts(world, readings, buildables, approximate) : [],
            wholeWorld ? PipeFlows.Of(snapshot, readings) : []);
    }

    /// <summary>
    /// BR-A17: the stocked input a machine or generator runs on, because its belts bring less than it uses, and how long
    /// that stock lasts. When several do, the one that runs out first.
    /// </summary>
    private static (string? Item, double? Minutes) OnStock(WorldGraph world, FlowReadings readings, MachineStateDocument state)
    {
        Buildable? running = world.Find(BuildableId.Of(state.Id));
        IReadOnlyDictionary<ItemId, decimal> stock = running switch
        {
            Machine machine => machine.InputStock,
            Consumer { Generation: not null } generator => generator.InputStock,
            _ => new Dictionary<ItemId, decimal>(),
        };
        IReadOnlyDictionary<ItemId, SharedKernel.Rate> demand = running?.Demand ?? new Dictionary<ItemId, SharedKernel.Rate>();

        (string? Item, double? Minutes) first = (null, null);
        foreach ((ItemId item, SharedKernel.Rate rate) in demand)
        {
            double perMinute = rate.ToDouble();
            if (!stock.TryGetValue(item, out decimal held) || perMinute <= 0 || (double)held < perMinute * SteadyState.ConfirmedMinutes)
            {
                continue;
            }

            double shortfall = perMinute * state.Utilisation - readings.Reaching(BuildableId.Of(state.Id), item);
            if (shortfall > SteadyState.Tolerance && (first.Minutes is null || (double)held / shortfall < first.Minutes))
            {
                first = (item.Id, Round((double)held / shortfall));
            }
        }

        return first;
    }

    /// <summary>
    /// BR-A16: every drone port where drones refuel at a known rate, the least supplied first, then every port with fuel
    /// in its slot where no drone refuels (issue #44).
    /// </summary>
    private static List<DronePortFinding> DronePorts(
        WorldGraph world, FlowReadings readings, Dictionary<string, BuildableDocument> buildables, HashSet<string> approximate)
    {
        List<(string Id, string Fuel, double? Burns)> ports = [];
        foreach (Buildable buildable in world.Buildables)
        {
            (IReadOnlySet<ItemId> burns, FuelBurn? burn) = buildable switch
            {
                Provider provider => (provider.Burns, provider.FuelBurn),
                Consumer consumer => (consumer.Burns, consumer.FuelBurn),
                _ => (new HashSet<ItemId>(), null),
            };
            switch (burn)
            {
                case FuelBurn.Known known:
                    ports.Add((buildable.Id.Id, burns.First().Id, known.PerMinute.ToDouble()));
                    break;
                case FuelBurn.NoDroneRefuels:
                    ports.Add((buildable.Id.Id, burns.First().Id, null));
                    break;
            }
        }

        return
        [
            .. ports
                .Select(port => (port.Id, port.Fuel, port.Burns, Supplied: readings.Reaching(BuildableId.Of(port.Id), ItemId.Of(port.Fuel))))
                .OrderBy(port => port.Burns is null).ThenBy(port => port.Burns is { } burns ? port.Supplied / burns : 0)
                .ThenBy(port => port.Id, StringComparer.Ordinal)
                .Select(port => new DronePortFinding(
                    port.Id,
                    buildables[port.Id].ClassName,
                    port.Fuel,
                    port.Burns is { } burns ? Round(burns) : null,
                    Round(port.Supplied),
                    buildables[port.Id].Position,
                    approximate.Contains(port.Id),
                    NoDroneRefuels: port.Burns is null))
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

    internal static double Round(double value) => Math.Round(value, Decimals, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Sources or destinations as the factory page shows them: each with its rate per minute rounded (BR-A10), and left
    /// out when that rounds to nothing (#52). The factory page and the arrangement both list them this way.
    /// </summary>
    internal static IEnumerable<(T Value, double PerMinute)> Shown<T>(IEnumerable<T> values, Func<T, double> perMinute) =>
        values.Select(value => (Value: value, PerMinute: Round(perMinute(value)))).Where(shown => shown.PerMinute > 0);
}