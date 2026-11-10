using SatisfactoryLedger.World.Contracts.Analysis;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow.Objects;

using Wire = SatisfactoryLedger.World.Contracts.Analysis;

namespace SatisfactoryLedger.World.Simulation;

/// <summary>
/// A flow network document, as World sends it (ADR-0018), turned back into the domain's network. The document is a
/// plain contract; reading it is this side's job.
/// </summary>
public static class DocumentToNetwork
{
    extension(FlowNetworkDocument document)
    {
        public FlowNetwork ToNetwork() => new(
            document.Nodes.Select(ToNode),
            document.Connections.Select(connection => new Connection(
                BuildableId.Of(connection.From), BuildableId.Of(connection.To), connection.FromPort, connection.ToPort)),
            document.Fluids.Select(ItemId.Of));
    }

    private static FlowNode ToNode(FlowNodeDocument node) => node switch
    {
        ProviderDocument provider => new FlowProvider(BuildableId.Of(provider.Id), ItemId.Of(provider.Item), provider.Offer),
        UnknownSourceDocument source => new FlowUnknownSource(
            BuildableId.Of(source.Id), Items(source.Stock), ToDomain(source.Carries), Items(source.Burns), source.BurnRate, ToDomain(source.Inventory),
            Items(source.Cargo)),
        MachineDocument machine => new FlowMachine(BuildableId.Of(machine.Id), Items(machine.Demand), Items(machine.Production), Items(machine.Stocked)),
        ConduitDocument conduit => new FlowConduit(BuildableId.Of(conduit.Id), conduit.Capacity ?? double.PositiveInfinity, ToDomain(conduit.Carries)),
        SplitterDocument splitter => new FlowSplitter(BuildableId.Of(splitter.Id), ToDomain(splitter.Carries),
            splitter.Rules?.Select(rule => new SortRule(rule.Output, ToDomain(rule.Kind), rule.Item is null ? null : ItemId.Of(rule.Item))).ToList()),
        MergerDocument merger => new FlowMerger(BuildableId.Of(merger.Id), ToDomain(merger.Carries), merger.Priorities),
        SinkDocument sink => new FlowSink(BuildableId.Of(sink.Id), Items(sink.Burns), sink.LeftoversOnly, sink.BurnRate, ToDomain(sink.Inventory)),
        _ => throw new ArgumentException($"{node} is not a kind of node this service knows", nameof(node)),
    };

    private static Inventory? ToDomain(InventoryDocument? inventory) => inventory is null ? null : new Inventory(
        inventory.Kind switch
        {
            Wire.InventoryKind.Slots => Domain.InventoryKind.Slots,
            Wire.InventoryKind.Fluid => Domain.InventoryKind.Fluid,
            Wire.InventoryKind.Depot => Domain.InventoryKind.Depot,
            _ => throw new ArgumentOutOfRangeException(nameof(inventory), inventory.Kind, null),
        },
        inventory.Size,
        inventory.Holds.ToDictionary(entry => ItemId.Of(entry.Key), entry => entry.Value),
        inventory.StackSizes.ToDictionary(entry => ItemId.Of(entry.Key), entry => entry.Value))
    {
        Arriving = (inventory.Arriving ?? new Dictionary<string, double>()).ToDictionary(entry => ItemId.Of(entry.Key), entry => entry.Value),
    };

    private static Dictionary<ItemId, double> Items(IReadOnlyDictionary<string, double> rates) =>
        rates.ToDictionary(rate => ItemId.Of(rate.Key), rate => rate.Value);

    private static HashSet<ItemId>? Items(IReadOnlyList<string>? names) => names is null ? null : [.. names.Select(ItemId.Of)];

    private static Domain.Flow.Objects.Carries ToDomain(Wire.Carries carries) => carries switch
    {
        Wire.Carries.Anything => Domain.Flow.Objects.Carries.Anything,
        Wire.Carries.Solids => Domain.Flow.Objects.Carries.Solids,
        Wire.Carries.Fluids => Domain.Flow.Objects.Carries.Fluids,
        _ => throw new ArgumentOutOfRangeException(nameof(carries), carries, null),
    };

    private static Domain.SortRuleKind ToDomain(Wire.SortRuleKind kind) => kind switch
    {
        Wire.SortRuleKind.Any => Domain.SortRuleKind.Any,
        Wire.SortRuleKind.AnyUndefined => Domain.SortRuleKind.AnyUndefined,
        Wire.SortRuleKind.Item => Domain.SortRuleKind.Item,
        Wire.SortRuleKind.Overflow => Domain.SortRuleKind.Overflow,
        Wire.SortRuleKind.None => Domain.SortRuleKind.None,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
