using SatisfactoryLedger.World.Contracts.Analysis;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Tracing;

using DomainLimit = SatisfactoryLedger.World.Domain.Flow.Objects.MachineLimit;

namespace SatisfactoryLedger.World.Analysis;

/// <summary>
/// A snapshot's stored analysis as every finding and trace reads it (decided 2026-11-26): the one place that turns the
/// stored document into <see cref="FlowReadings"/>.
/// </summary>
internal static class AnalysisReadings
{
    public static FlowReadings Of(WorldGraph world, AnalysisDocument analysis)
    {
        static IReadOnlyDictionary<ItemId, double> Items(IReadOnlyDictionary<string, double>? shares) =>
            (shares ?? new Dictionary<string, double>()).ToDictionary(share => ItemId.Of(share.Key), share => share.Value);

        return new FlowReadings(
            world,
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
}
