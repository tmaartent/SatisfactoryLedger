using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.Snapshots;

using SatisfactoryLedger.World.Simulation;
namespace SatisfactoryLedger.World.Analysis;

/// <summary>How the machines of a snapshot run: at 100%, or below it and why (BR-A11).</summary>
internal sealed record AnalysisSummary(int Machines, int Running, int Starved, int OutputBlocked, int NoRecipe);

/// <summary>A connected part of the world that didn't settle: its findings are approximate (BR-A10).</summary>
/// <param name="Example">One of its buildables, to find the part in the game.</param>
internal sealed record ApproximatePart(int Buildables, int Sweeps, string Example);

/// <summary>A machine, how fast it runs, and why not faster when below 100% (BR-A11).</summary>
/// <param name="Utilisation">Between 0 and 1, to 4 decimals.</param>
/// <param name="LimitingItem">The item it's starved of or blocked on; null when it runs or has no known recipe.</param>
/// <param name="Approximate">True when its part of the world didn't settle (BR-A10).</param>
/// <param name="OnStock">
/// The input it runs on from what it holds, because its belts bring less than it uses (BR-A17); null when it doesn't.
/// </param>
/// <param name="StockMinutes">How long that stock lasts at the rate it's used up; null when it doesn't run on stock.</param>
internal sealed record MachineFinding(
    string Id,
    string ClassName,
    string? Recipe,
    double Utilisation,
    MachineLimit Limit,
    string? LimitingItem,
    PositionDocument? Position,
    bool Approximate,
    string? OnStock = null,
    double? StockMinutes = null);

/// <summary>A belt, lift or pipe at its capacity while more is offered upstream (BR-A12). Rates per minute.</summary>
internal sealed record BottleneckFinding(
    string Id,
    string ClassName,
    double Capacity,
    double Flow,
    double OfferedMore,
    PositionDocument? Position,
    bool Approximate);

/// <summary>A provider delivering less than it offers (BR-A13). Rates per minute.</summary>
internal sealed record UnusedSupplyFinding(
    string Id,
    string ClassName,
    string Item,
    double Offered,
    double Delivered,
    PositionDocument? Position,
    bool Approximate);

/// <summary>A source whose rate isn't known, and how long its stock lasts (BR-A9).</summary>
/// <param name="Delivered">Per item, what it delivers per minute.</param>
/// <param name="StockMinutes">How long its stock lasts at that rate; null when its stock doesn't hold what it delivers.</param>
/// <param name="Confirmed">True when the stock lasts at least 30 minutes; otherwise it's assumed supplied.</param>
internal sealed record UnknownSupplyFinding(
    string Id,
    string ClassName,
    IReadOnlyDictionary<string, double> Delivered,
    double? StockMinutes,
    bool Confirmed,
    PositionDocument? Position,
    bool Approximate);

/// <summary>A generator burning a known fuel (BR-A15): how fast it runs, and why not faster when below full power.</summary>
/// <param name="Fuel">The fuel it burns: the one in it at save time.</param>
/// <param name="Utilisation">Between 0 and 1, to 4 decimals.</param>
/// <param name="LimitingItem">The fuel or water it's starved of, or the waste it's blocked on; null when it runs.</param>
/// <param name="OnStock">The fuel it burns from what it holds, as for a machine (BR-A17); null when it doesn't.</param>
internal sealed record GeneratorFinding(
    string Id,
    string ClassName,
    string Fuel,
    double Utilisation,
    MachineLimit Limit,
    string? LimitingItem,
    PositionDocument? Position,
    bool Approximate,
    string? OnStock = null,
    double? StockMinutes = null);

/// <summary>A drone port where drones refuel, what they burn there, and what fuel reaches it (BR-A16). Rates per minute.</summary>
/// <param name="Fuel">The fuel in its slot at save time.</param>
/// <param name="Burns">What the drones refuelling there burn, from their latest trips.</param>
/// <param name="Supplied">What its belts bring of that fuel.</param>
internal sealed record DronePortFinding(
    string Id,
    string ClassName,
    string Fuel,
    double Burns,
    double Supplied,
    PositionDocument? Position,
    bool Approximate);

/// <summary>The findings of a snapshot: worst first in every list.</summary>
/// <param name="Settled">False when a part of the world didn't settle; its findings are marked approximate.</param>
internal sealed record AnalysisResponse(
    bool Settled,
    AnalysisSummary Summary,
    IReadOnlyList<ApproximatePart> ApproximateParts,
    IReadOnlyList<MachineFinding> Machines,
    IReadOnlyList<BottleneckFinding> Bottlenecks,
    IReadOnlyList<UnusedSupplyFinding> UnusedSupply,
    IReadOnlyList<UnknownSupplyFinding> UnknownSupply,
    IReadOnlyList<GeneratorFinding> Generators,
    IReadOnlyList<DronePortFinding> DronePorts);

/// <summary>What a factory makes of one item: on its output belt now, and if its machines all ran at 100% (BR-A14).</summary>
/// <param name="Now">What flows onto the output belt per minute.</param>
/// <param name="AtFullSpeed">What the machines that feed the output belt make per minute at 100%, at their clock speeds.</param>
internal sealed record FactoryOutput(string Item, double Now, double AtFullSpeed);

/// <summary>
/// The findings of one factory (BR-A14): its own machines and belts, and the suppliers that feed it. A supplier shared
/// with another factory is in both.
/// </summary>
internal sealed record FactoryAnalysisResponse(
    Guid FactoryId,
    string Name,
    bool Settled,
    AnalysisSummary Summary,
    IReadOnlyList<FactoryOutput> Output,
    IReadOnlyList<MachineFinding> Machines,
    IReadOnlyList<BottleneckFinding> Bottlenecks,
    IReadOnlyList<UnusedSupplyFinding> UnusedSupply,
    IReadOnlyList<UnknownSupplyFinding> UnknownSupply);

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
        FactoryDefinition definition = factory.ToDefinition();
        if (!definition.IsTraceableIn(world))
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
            OutputOf(world, members, definition, analysis),
            findings.Machines,
            findings.Bottlenecks,
            findings.UnusedSupply,
            findings.UnknownSupply);
    }

    /// <summary>Per item the producers make: what flows onto the output belt now, and what they'd make at 100%.</summary>
    private static List<FactoryOutput> OutputOf(WorldGraph world, FactoryMembers members, FactoryDefinition definition, AnalysisDocument analysis)
    {
        IReadOnlyDictionary<string, double> now = analysis.Into.GetValueOrDefault(definition.OutputBelt.Id) ?? new Dictionary<string, double>();
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

    private static double Round(double value) => Math.Round(value, Decimals, MidpointRounding.AwayFromZero);
}
