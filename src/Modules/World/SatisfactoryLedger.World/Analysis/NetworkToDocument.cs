using SatisfactoryLedger.World.Contracts.Analysis;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow.Objects;

using Wire = SatisfactoryLedger.World.Contracts.Analysis;

namespace SatisfactoryLedger.World.Analysis;

/// <summary>
/// A snapshot's flow network as the document World sends the analysis service (ADR-0018): every node with its rates,
/// every connection with its ports, and which items are fluids.
/// </summary>
internal static class NetworkToDocument
{
    extension(FlowNetworkDocument)
    {
        public static FlowNetworkDocument From(FlowNetwork network)
        {
            ArgumentNullException.ThrowIfNull(network);

            return new FlowNetworkDocument(
                FlowNetworkDocument.CurrentVersion,
                [.. network.Nodes.Select(ToDocument)],
                [
                    .. Enumerable.Range(0, network.Edges.Count).Select(edge => new FlowConnectionDocument(
                        network.Nodes[network.Edges[edge].From].Id.Id,
                        network.Nodes[network.Edges[edge].To].Id.Id,
                        network.Ports[edge].From,
                        network.Ports[edge].To))
                ],
                [.. network.Fluids.Select(item => item.Id).Order(StringComparer.Ordinal)]);
        }
    }

    private static FlowNodeDocument ToDocument(FlowNode node) => node switch
    {
        FlowProvider provider => new ProviderDocument(provider.Id.Id, provider.Item.Id, provider.Offer),
        FlowUnknownSource source => new UnknownSourceDocument(
            source.Id.Id, Items(source.Stock), ToWire(source.Carries), Names(source.Burns), source.BurnRate, ToWire(source.Inventory), Names(source.Cargo)),
        FlowMachine machine => new MachineDocument(machine.Id.Id, Items(machine.Demand), Items(machine.Production), Names(machine.Stocked)),
        // JSON has no infinity: a conduit without a limit has no capacity.
        FlowConduit conduit => new ConduitDocument(conduit.Id.Id, double.IsPositiveInfinity(conduit.Capacity) ? null : conduit.Capacity, ToWire(conduit.Carries)),
        FlowSplitter splitter => new SplitterDocument(splitter.Id.Id, ToWire(splitter.Carries),
            splitter.Rules is { Count: > 0 } rules ? [.. rules.Select(rule => new Wire.SortRuleDocument(rule.Output, ToWire(rule.Kind), rule.Item?.Id))] : null),
        FlowMerger merger => new MergerDocument(merger.Id.Id, ToWire(merger.Carries), merger.Priorities is { Count: > 0 } priorities ? [.. priorities] : null),
        FlowSink sink => new SinkDocument(sink.Id.Id, Names(sink.Burns), sink.LeftoversOnly, sink.BurnRate, ToWire(sink.Inventory)),
        _ => throw new ArgumentException($"{node} is not a kind of node the document knows", nameof(node)),
    };

    private static InventoryDocument? ToWire(Inventory? inventory) => inventory is null ? null : new InventoryDocument(
        inventory.Kind switch
        {
            Domain.InventoryKind.Slots => Wire.InventoryKind.Slots,
            Domain.InventoryKind.Fluid => Wire.InventoryKind.Fluid,
            Domain.InventoryKind.Depot => Wire.InventoryKind.Depot,
            _ => throw new ArgumentOutOfRangeException(nameof(inventory), inventory.Kind, null),
        },
        inventory.Size,
        inventory.Holds.ToDictionary(entry => entry.Key.Id, entry => entry.Value, StringComparer.Ordinal),
        inventory.StackSizes.ToDictionary(entry => entry.Key.Id, entry => entry.Value, StringComparer.Ordinal),
        inventory.Arriving.Count > 0 ? inventory.Arriving.ToDictionary(entry => entry.Key.Id, entry => entry.Value, StringComparer.Ordinal) : null);

    private static Dictionary<string, double> Items(IReadOnlyDictionary<ItemId, double> rates) =>
        rates.ToDictionary(rate => rate.Key.Id, rate => rate.Value, StringComparer.Ordinal);

    private static List<string>? Names(IReadOnlySet<ItemId>? items) =>
        items is { Count: > 0 } ? [.. items.Select(item => item.Id).Order(StringComparer.Ordinal)] : null;

    private static Wire.Carries ToWire(Domain.Flow.Objects.Carries carries) => carries switch
    {
        Domain.Flow.Objects.Carries.Anything => Wire.Carries.Anything,
        Domain.Flow.Objects.Carries.Solids => Wire.Carries.Solids,
        Domain.Flow.Objects.Carries.Fluids => Wire.Carries.Fluids,
        _ => throw new ArgumentOutOfRangeException(nameof(carries), carries, null),
    };

    private static Wire.SortRuleKind ToWire(Domain.SortRuleKind kind) => kind switch
    {
        Domain.SortRuleKind.Any => Wire.SortRuleKind.Any,
        Domain.SortRuleKind.AnyUndefined => Wire.SortRuleKind.AnyUndefined,
        Domain.SortRuleKind.Item => Wire.SortRuleKind.Item,
        Domain.SortRuleKind.Overflow => Wire.SortRuleKind.Overflow,
        Domain.SortRuleKind.None => Wire.SortRuleKind.None,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
