using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Analysis;

/// <summary>How the machines of a snapshot run: at 100%, or below it and why (BR-A11).</summary>
internal sealed record AnalysisSummary(int Machines, int Running, int Starved, int OutputBlocked, int NoRecipe);

/// <summary>A connected part of the world that didn't settle: its findings are approximate (BR-A10).</summary>
/// <param name="Example">One of its buildables, to find the part in the game.</param>
internal sealed record ApproximatePart(int Buildables, int Sweeps, string Example);

/// <summary>A machine below 100% (BR-A11).</summary>
/// <param name="Utilisation">Between 0 and 1, to 4 decimals.</param>
/// <param name="LimitingItem">The item it's starved of or blocked on; null when it has no known recipe.</param>
/// <param name="Approximate">True when its part of the world didn't settle (BR-A10).</param>
internal sealed record MachineFinding(
    string Id,
    string ClassName,
    string? Recipe,
    double Utilisation,
    MachineLimit Limit,
    string? LimitingItem,
    PositionDocument? Position,
    bool Approximate);

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

/// <summary>The findings of a snapshot: worst first in every list.</summary>
/// <param name="Settled">False when a part of the world didn't settle; its findings are marked approximate.</param>
internal sealed record AnalysisResponse(
    bool Settled,
    AnalysisSummary Summary,
    IReadOnlyList<ApproximatePart> ApproximateParts,
    IReadOnlyList<MachineFinding> Machines,
    IReadOnlyList<BottleneckFinding> Bottlenecks,
    IReadOnlyList<UnusedSupplyFinding> UnusedSupply,
    IReadOnlyList<UnknownSupplyFinding> UnknownSupply);

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

/// <summary>Runs the flow analysis on a stored snapshot and turns its result into findings a player can act on.</summary>
internal static class SnapshotAnalysis
{
    /// <summary>Results are meaningful to 0.0001 a minute, so that's what they show (BR-A10).</summary>
    private const int Decimals = 4;

    /// <summary>The findings of the whole world.</summary>
    public static AnalysisResponse For(SnapshotDocument snapshot)
    {
        FlowResult result = SteadyState.Of(snapshot.ToFlowNetwork());
        return Findings(snapshot, result, result.Parts, _ => true, _ => true, _ => true);
    }

    /// <summary>
    /// The findings of one factory. The whole world is simulated, because a factory depends on its supply; the findings
    /// are then its machines and logistics, and its suppliers (BR-A14). Null when the factory can't be traced in this
    /// snapshot (BR-W11).
    /// </summary>
    public static FactoryAnalysisResponse? ForFactory(SnapshotDocument snapshot, Factories.StoredFactory factory)
    {
        WorldGraph world = snapshot.ToWorldGraph();
        FactoryDefinition definition = factory.ToDefinition();
        if (!definition.IsTraceableIn(world))
        {
            return null;
        }

        FactoryMembers members = definition.TraceMembers(world);
        FlowResult result = SteadyState.Of(WorldFlow.From(world, snapshot.Fluids.Select(ItemId.Of)));
        HashSet<FlowPart> parts = [.. members.Machines.Concat(members.Logistics).Select(result.PartOf)];
        AnalysisResponse findings = Findings(snapshot, result, parts, members.Machines.Contains, members.Logistics.Contains, members.Suppliers.Contains);

        return new FactoryAnalysisResponse(
            factory.Id,
            factory.Name,
            findings.Settled,
            findings.Summary,
            OutputOf(world, members, definition, result),
            findings.Machines,
            findings.Bottlenecks,
            findings.UnusedSupply,
            findings.UnknownSupply);
    }

    /// <summary>Per item the producers make: what flows onto the output belt now, and what they'd make at 100%.</summary>
    private static List<FactoryOutput> OutputOf(WorldGraph world, FactoryMembers members, FactoryDefinition definition, FlowResult result)
    {
        IReadOnlyDictionary<ItemId, double> now = result.Into(definition.OutputBelt);
        Dictionary<ItemId, double> atFullSpeed = [];
        foreach (Machine producer in members.Producers.Select(id => world.Find(id)).OfType<Machine>())
        {
            foreach ((ItemId item, SharedKernel.Rate rate) in producer.Production)
            {
                atFullSpeed[item] = atFullSpeed.GetValueOrDefault(item) + rate.ToDouble();
            }
        }

        return
        [
            .. atFullSpeed.Keys.Union(now.Keys).OrderBy(item => item.Id, StringComparer.Ordinal)
                .Select(item => new FactoryOutput(item.Id, Round(now.GetValueOrDefault(item)), Round(atFullSpeed.GetValueOrDefault(item))))
        ];
    }

    /// <summary>
    /// The findings of the machines, conduits and suppliers the filters let through; worst first. Whether they settled
    /// is read from <paramref name="parts"/>: the parts of the world they're in.
    /// </summary>
    private static AnalysisResponse Findings(
        SnapshotDocument snapshot,
        FlowResult result,
        IReadOnlyCollection<FlowPart> parts,
        Func<BuildableId, bool> isMachine,
        Func<BuildableId, bool> isConduit,
        Func<BuildableId, bool> isSupplier)
    {
        Dictionary<string, BuildableDocument> buildables = snapshot.Buildables.ToDictionary(buildable => buildable.Id, StringComparer.Ordinal);
        BuildableDocument Of(BuildableId id) => buildables[id.Id];
        bool Approximate(BuildableId id) => !result.PartOf(id).Settled;

        List<MachineState> machines = [.. result.Machines.Where(machine => isMachine(machine.Machine))];
        return new AnalysisResponse(
            parts.All(part => part.Settled),
            new AnalysisSummary(
                machines.Count,
                machines.Count(machine => machine.Limit == MachineLimit.None),
                machines.Count(machine => machine.Limit == MachineLimit.Starved),
                machines.Count(machine => machine.Limit == MachineLimit.OutputBlocked),
                machines.Count(machine => machine.Limit == MachineLimit.NoRecipe)),
            [
                .. parts.Where(part => !part.Settled)
                    .OrderByDescending(part => part.Buildables.Count)
                    .Select(part => new ApproximatePart(part.Buildables.Count, part.Sweeps, part.Buildables[0].Id))
            ],
            [
                .. machines.Where(machine => machine.Limit != MachineLimit.None)
                    .OrderBy(machine => machine.Utilisation).ThenBy(machine => machine.Machine.Id, StringComparer.Ordinal)
                    .Select(machine => new MachineFinding(
                        machine.Machine.Id,
                        Of(machine.Machine).ClassName,
                        Of(machine.Machine).Recipe,
                        Round(machine.Utilisation),
                        machine.Limit,
                        machine.LimitingItem?.Id,
                        Of(machine.Machine).Position,
                        Approximate(machine.Machine)))
            ],
            [
                .. result.Bottlenecks.Where(bottleneck => isConduit(bottleneck.Conduit))
                    .OrderByDescending(bottleneck => bottleneck.OfferedMore).ThenBy(bottleneck => bottleneck.Conduit.Id, StringComparer.Ordinal)
                    .Select(bottleneck => new BottleneckFinding(
                        bottleneck.Conduit.Id,
                        Of(bottleneck.Conduit).ClassName,
                        Round(bottleneck.Capacity),
                        Round(bottleneck.Flow),
                        Round(bottleneck.OfferedMore),
                        Of(bottleneck.Conduit).Position,
                        Approximate(bottleneck.Conduit)))
            ],
            [
                .. result.UnusedSupply.Where(supply => isSupplier(supply.Provider))
                    .OrderByDescending(supply => supply.Offered - supply.Delivered).ThenBy(supply => supply.Provider.Id, StringComparer.Ordinal)
                    .Select(supply => new UnusedSupplyFinding(
                        supply.Provider.Id,
                        Of(supply.Provider).ClassName,
                        supply.Item.Id,
                        Round(supply.Offered),
                        Round(supply.Delivered),
                        Of(supply.Provider).Position,
                        Approximate(supply.Provider)))
            ],
            [
                // Assumed supplied first, the shortest stock first: those need checking in the game.
                .. result.UnknownSupply.Where(supply => isSupplier(supply.Source))
                    .OrderBy(supply => supply.Confirmed).ThenBy(supply => supply.StockMinutes ?? -1).ThenBy(supply => supply.Source.Id, StringComparer.Ordinal)
                    .Select(supply => new UnknownSupplyFinding(
                        supply.Source.Id,
                        Of(supply.Source).ClassName,
                        supply.Delivered.OrderBy(entry => entry.Key.Id, StringComparer.Ordinal).ToDictionary(entry => entry.Key.Id, entry => Round(entry.Value)),
                        supply.StockMinutes is { } minutes ? Round(minutes) : null,
                        supply.Confirmed,
                        Of(supply.Source).Position,
                        Approximate(supply.Source)))
            ]);
    }

    private static double Round(double value) => Math.Round(value, Decimals, MidpointRounding.AwayFromZero);
}
