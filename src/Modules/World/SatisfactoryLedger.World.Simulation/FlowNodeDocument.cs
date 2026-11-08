using System.Text.Json.Serialization;

using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;

namespace SatisfactoryLedger.World.Simulation;

/// <summary>One node of a flow network. <c>kind</c> in the JSON says which (System.Text.Json polymorphism).</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ProviderDocument), "provider")]
[JsonDerivedType(typeof(UnknownSourceDocument), "unknownSource")]
[JsonDerivedType(typeof(MachineDocument), "machine")]
[JsonDerivedType(typeof(ConduitDocument), "conduit")]
[JsonDerivedType(typeof(SplitterDocument), "splitter")]
[JsonDerivedType(typeof(MergerDocument), "merger")]
[JsonDerivedType(typeof(SinkDocument), "sink")]
public abstract record FlowNodeDocument(string Id)
{
    public abstract FlowNode ToNode();

    public static FlowNodeDocument From(FlowNode node) => node switch
    {
        FlowProvider provider => new ProviderDocument(provider.Id.Id, provider.Item.Id, provider.Offer),
        FlowUnknownSource source => new UnknownSourceDocument(source.Id.Id, Items(source.Stock), source.Carries, Names(source.Burns), source.BurnRate),
        FlowMachine machine => new MachineDocument(machine.Id.Id, Items(machine.Demand), Items(machine.Production), Names(machine.Stocked)),
        // JSON has no infinity: a conduit without a limit has no capacity.
        FlowConduit conduit => new ConduitDocument(conduit.Id.Id, double.IsPositiveInfinity(conduit.Capacity) ? null : conduit.Capacity, conduit.Carries),
        FlowSplitter splitter => new SplitterDocument(splitter.Id.Id, splitter.Carries,
            splitter.Rules is { Count: > 0 } rules ? [.. rules.Select(rule => new SortRuleDocument(rule.Output, rule.Kind, rule.Item?.Id))] : null),
        FlowMerger merger => new MergerDocument(merger.Id.Id, merger.Carries, merger.Priorities is { Count: > 0 } priorities ? [.. priorities] : null),
        FlowSink sink => new SinkDocument(sink.Id.Id, Names(sink.Burns), sink.LeftoversOnly, sink.BurnRate),
        _ => throw new ArgumentException($"{node} is not a kind of node the document knows", nameof(node)),
    };

    private protected static Dictionary<string, double> Items(IReadOnlyDictionary<ItemId, double> rates) =>
        rates.ToDictionary(rate => rate.Key.Id, rate => rate.Value, StringComparer.Ordinal);

    private protected static Dictionary<ItemId, double> Items(IReadOnlyDictionary<string, double> rates) =>
        rates.ToDictionary(rate => ItemId.Of(rate.Key), rate => rate.Value);

    private protected static List<string>? Names(IReadOnlySet<ItemId>? items) =>
        items is { Count: > 0 } ? [.. items.Select(item => item.Id).Order(StringComparer.Ordinal)] : null;

    private protected static HashSet<ItemId>? Items(IReadOnlyList<string>? names) => names is null ? null : [.. names.Select(ItemId.Of)];
}