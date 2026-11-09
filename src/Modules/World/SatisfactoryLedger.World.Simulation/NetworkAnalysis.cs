using SatisfactoryLedger.World.Contracts.Analysis;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.Domain.Flow.Objects;

using Wire = SatisfactoryLedger.World.Contracts.Analysis;

namespace SatisfactoryLedger.World.Simulation;

/// <summary>Simulates a world's flow network and keeps what the findings need, as an analysis document (ADR-0017).</summary>
public static class NetworkAnalysis
{
    extension(AnalysisDocument)
    {
        public static AnalysisDocument Compute(FlowNetwork network)
        {
            ArgumentNullException.ThrowIfNull(network);
            FlowResult result = SteadyState.Of(network);

            Dictionary<string, IReadOnlyDictionary<string, double>> into = new(StringComparer.Ordinal);
            foreach (FlowConduit conduit in network.Nodes.OfType<FlowConduit>())
            {
                Dictionary<string, double> flowing = result.Into(conduit.Id)
                    .Where(entry => entry.Value > SteadyState.Tolerance)
                    .ToDictionary(entry => entry.Key.Id, entry => entry.Value, StringComparer.Ordinal);
                if (flowing.Count > 0)
                {
                    into[conduit.Id.Id] = flowing;
                }
            }

            return new AnalysisDocument(
                AnalysisDocument.CurrentVersion,
                [
                    .. result.Machines.Select(machine => new MachineStateDocument(
                        machine.Machine.Id, machine.Utilisation, ToWire(machine.Limit), machine.LimitingItem?.Id, Names(machine.StarvedOf), Names(machine.BlockedOn)))
                ],
                [.. result.Bottlenecks.Select(bottleneck => new BottleneckDocument(bottleneck.Conduit.Id, bottleneck.Capacity, bottleneck.Flow, bottleneck.OfferedMore))],
                [.. result.UnusedSupply.Select(supply => new UnusedSupplyDocument(supply.Provider.Id, supply.Item.Id, supply.Offered, supply.Delivered))],
                [
                    .. result.UnknownSupply.Select(supply => new UnknownSupplyDocument(
                        supply.Source.Id,
                        supply.Delivered.ToDictionary(entry => entry.Key.Id, entry => entry.Value, StringComparer.Ordinal),
                        supply.StockMinutes,
                        supply.Confirmed))
                ],
                [.. result.Parts.Where(part => !part.Settled).Select(part => new UnsettledPartDocument([.. part.Buildables.Select(id => id.Id)], part.Sweeps))],
                into);
        }
    }

    private static Dictionary<string, double>? Names(IReadOnlyDictionary<ItemId, double>? shares) =>
        shares?.ToDictionary(share => share.Key.Id, share => share.Value, StringComparer.Ordinal);

    private static Wire.MachineLimit ToWire(Domain.Flow.Objects.MachineLimit limit) => limit switch
    {
        Domain.Flow.Objects.MachineLimit.None => Wire.MachineLimit.None,
        Domain.Flow.Objects.MachineLimit.Starved => Wire.MachineLimit.Starved,
        Domain.Flow.Objects.MachineLimit.OutputBlocked => Wire.MachineLimit.OutputBlocked,
        Domain.Flow.Objects.MachineLimit.NoRecipe => Wire.MachineLimit.NoRecipe,
        _ => throw new ArgumentOutOfRangeException(nameof(limit), limit, null),
    };
}
