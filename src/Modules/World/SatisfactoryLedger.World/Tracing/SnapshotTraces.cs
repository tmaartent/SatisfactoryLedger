using SatisfactoryLedger.Catalog.Contracts;
using SatisfactoryLedger.World.Analysis;
using SatisfactoryLedger.World.Contracts.Analysis;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Tracing;
using SatisfactoryLedger.World.SaveFile;
using SatisfactoryLedger.World.Snapshots;

using DomainLimit = SatisfactoryLedger.World.Domain.Flow.Objects.MachineLimit;

namespace SatisfactoryLedger.World.Tracing;

/// <summary>
/// Where the shortage or blockage of a kind of machine starts, in a snapshot (decided 2026-11-20): the machines of one
/// class and recipe, traced together over the snapshot's stored analysis, with the buildables on the way grouped by
/// class and recipe.
/// </summary>
internal static class SnapshotTraces
{
    /// <summary>How many ids a group names: enough to find them, few enough to read.</summary>
    private const int IdsShown = 10;

    /// <param name="recipe">Only the machines with this recipe; all of the class when null.</param>
    /// <param name="tiers">The game's belt, lift and pipe tiers, for what a bottleneck needs (decided 2026-11-22).</param>
    public static TraceResponse Shortage(SnapshotDocument snapshot, AnalysisDocument analysis, string building, string? recipe, IReadOnlyCollection<TransportTier> tiers) =>
        Trace(snapshot, analysis, building, recipe, MachineLimit.Starved, (world, readings, machines) => ShortageTrace.From(world, readings, machines, tiers));

    public static TraceResponse Blockage(SnapshotDocument snapshot, AnalysisDocument analysis, string building, string? recipe, IReadOnlyCollection<TransportTier> tiers) =>
        Trace(snapshot, analysis, building, recipe, MachineLimit.OutputBlocked, (world, readings, machines) => BlockageTrace.From(world, readings, machines, tiers));

    /// <summary>The Catalog's tiers as a trace reads them, by kind. A class that isn't a belt, lift or pipe is left out.</summary>
    public static IReadOnlyList<TransportTier> Tiers(IEnumerable<CatalogTransportTier> tiers) =>
    [
        .. tiers
            .Select(tier => (tier, Kind: GameClasses.TryLogisticsKindOf(tier.Id)))
            .Where(tier => tier.Kind is LogisticsKind.Belt or LogisticsKind.Lift or LogisticsKind.Pipe)
            .Select(tier => new TransportTier(tier.tier.Id, tier.Kind!.Value, tier.tier.CapacityPerMinute)),
    ];

    private static TraceResponse Trace(
        SnapshotDocument snapshot,
        AnalysisDocument analysis,
        string building,
        string? recipe,
        MachineLimit traced,
        Func<WorldGraph, FlowReadings, IReadOnlyCollection<BuildableId>, IReadOnlyList<TraceStart>> trace)
    {
        Dictionary<string, BuildableDocument> buildables = snapshot.Buildables.ToDictionary(buildable => buildable.Id, StringComparer.Ordinal);
        Dictionary<string, MachineStateDocument> states = analysis.Machines.ToDictionary(state => state.Id, StringComparer.Ordinal);
        List<BuildableId> machines =
        [
            .. snapshot.Buildables
                .Where(buildable => buildable.ClassName == building && (recipe is null || buildable.Recipe == recipe) && states.ContainsKey(buildable.Id))
                .Select(buildable => BuildableId.Of(buildable.Id)),
        ];
        int tracedCount = machines.Count(id => states[id.Id].Limit == traced);
        IReadOnlyList<TraceStart> starts = trace(snapshot.ToWorldGraph(), Readings(analysis), machines);
        return new TraceResponse(building, recipe, machines.Count, tracedCount, [.. starts.Select(start => Start(start, buildables))]);
    }

    /// <summary>The stored analysis as a trace reads it.</summary>
    internal static FlowReadings Readings(AnalysisDocument analysis)
    {
        static IReadOnlyDictionary<ItemId, double> Items(IReadOnlyDictionary<string, double>? shares) =>
            (shares ?? new Dictionary<string, double>()).ToDictionary(share => ItemId.Of(share.Key), share => share.Value);

        return new FlowReadings(
            analysis.Machines.ToDictionary(
                state => BuildableId.Of(state.Id),
                state => new MachineReading(Enum.Parse<DomainLimit>(state.Limit.ToString()), state.Utilisation, Items(state.StarvedOf), Items(state.BlockedOn))),
            analysis.Bottlenecks.ToDictionary(bottleneck => BuildableId.Of(bottleneck.Id), bottleneck => bottleneck.OfferedMore),
            analysis.UnusedSupply
                .GroupBy(supply => supply.Id, StringComparer.Ordinal)
                .ToDictionary(
                    group => BuildableId.Of(group.Key),
                    group => (IReadOnlyDictionary<ItemId, double>)group.ToDictionary(supply => ItemId.Of(supply.Item), supply => supply.Offered - supply.Delivered)),
            analysis.UnknownSupply.ToDictionary(supply => BuildableId.Of(supply.Id), supply => Items(supply.Delivered)),
            analysis.Into.ToDictionary(entry => BuildableId.Of(entry.Key), entry => Items(entry.Value)));
    }

    private static TraceStartResponse Start(TraceStart start, IReadOnlyDictionary<string, BuildableDocument> buildables) => new(
        start.Cause,
        start.Item.Id,
        Groups(start.At.Select(id => (id, 0.0)), buildables),
        [.. start.Path.Select(step => new TraceStepResponse(Groups(step.Machines.Select(id => (id, 0.0)), buildables), step.Item.Id, SnapshotAnalysis.Round(step.PerMinute)))],
        SnapshotAnalysis.Round(start.PerMinute),
        Groups(start.Others.Select(other => (other.Key, other.Value)), buildables),
        start.ShortOf.ToDictionary(share => share.Key.Id, share => SnapshotAnalysis.Round(share.Value)),
        start.MoreCapacity is { } more ? more with { Carries = SnapshotAnalysis.Round(more.Carries) } : null);

    /// <summary>Buildables of one class and recipe together, the biggest group first.</summary>
    private static IReadOnlyList<TraceGroupResponse> Groups(IEnumerable<(BuildableId Id, double PerMinute)> members, IReadOnlyDictionary<string, BuildableDocument> buildables) =>
    [
        .. members
            .Select(member => (member.Id.Id, member.PerMinute, Buildable: buildables.GetValueOrDefault(member.Id.Id)))
            .GroupBy(member => (ClassName: member.Buildable?.ClassName ?? member.Id, member.Buildable?.Recipe))
            .Select(group => new TraceGroupResponse(
                group.Key.ClassName,
                group.Key.Recipe,
                group.Count(),
                SnapshotAnalysis.Round(group.Sum(member => member.PerMinute)),
                [.. group.Select(member => member.Id).Take(IdsShown)],
                group.Select(member => member.Buildable?.Position).FirstOrDefault(position => position is not null)))
            .OrderByDescending(group => group.Count)
            .ThenByDescending(group => group.PerMinute),
    ];
}
