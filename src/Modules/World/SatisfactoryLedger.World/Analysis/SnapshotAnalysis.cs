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

/// <summary>A generator burning a known fuel (BR-A15): how fast it runs, and why not faster when below full power.</summary>
/// <param name="Fuel">The fuel it burns: the one in it at save time.</param>
/// <param name="Utilisation">Between 0 and 1, to 4 decimals.</param>
/// <param name="LimitingItem">The fuel or water it's starved of, or the waste it's blocked on; null when it runs.</param>
internal sealed record GeneratorFinding(
    string Id,
    string ClassName,
    string Fuel,
    double Utilisation,
    MachineLimit Limit,
    string? LimitingItem,
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
    IReadOnlyList<GeneratorFinding> Generators);

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
        Findings(snapshot, analysis, analysis.UnsettledParts, _ => true, _ => true, _ => true, withGenerators: true);

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
            // A generator is a consumer, never part of a factory (BR-W8).
            withGenerators: false);

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
    private static AnalysisResponse Findings(
        SnapshotDocument snapshot,
        AnalysisDocument analysis,
        IReadOnlyCollection<UnsettledPartDocument> unsettled,
        Func<string, bool> isMachine,
        Func<string, bool> isConduit,
        Func<string, bool> isSupplier,
        bool withGenerators)
    {
        Dictionary<string, BuildableDocument> buildables = snapshot.Buildables.ToDictionary(buildable => buildable.Id, StringComparer.Ordinal);
        HashSet<string> approximate = analysis.Approximate();

        // The flow simulates generators like machines (BR-A8), but they're consumers: their own findings (BR-A15).
        List<MachineStateDocument> machines = [.. analysis.Machines
            .Where(machine => buildables[machine.Id].Role == BuildableDocumentRole.Machine && isMachine(machine.Id))];
        List<MachineStateDocument> generators = withGenerators
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
                        approximate.Contains(machine.Id)))
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
                        approximate.Contains(generator.Id)))
            ]);
    }

    private static double Round(double value) => Math.Round(value, Decimals, MidpointRounding.AwayFromZero);
}
