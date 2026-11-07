using System.Text.Json;
using System.Text.Json.Serialization;

using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;

namespace SatisfactoryLedger.World.Simulation;

/// <summary>
/// A flow network as JSON: what World sends the analysis service (ADR-0018). Every node with its rates, every
/// connection with its ports, and which items are fluids.
/// </summary>
public sealed record FlowNetworkDocument(
    int Version,
    IReadOnlyList<FlowNodeDocument> Nodes,
    IReadOnlyList<FlowConnectionDocument> Connections,
    IReadOnlyList<string> Fluids)
{
    public const int CurrentVersion = 1;

    public static FlowNetworkDocument From(FlowNetwork network)
    {
        ArgumentNullException.ThrowIfNull(network);

        return new FlowNetworkDocument(
            CurrentVersion,
            [.. network.Nodes.Select(FlowNodeDocument.From)],
            [
                .. Enumerable.Range(0, network.Edges.Count).Select(edge => new FlowConnectionDocument(
                    network.Nodes[network.Edges[edge].From].Id.Id,
                    network.Nodes[network.Edges[edge].To].Id.Id,
                    network.Ports[edge].From,
                    network.Ports[edge].To))
            ],
            [.. network.Fluids.Select(item => item.Id).Order(StringComparer.Ordinal)]);
    }

    public FlowNetwork ToNetwork() => new(
        Nodes.Select(node => node.ToNode()),
        Connections.Select(connection => new Connection(
            BuildableId.Of(connection.From), BuildableId.Of(connection.To), connection.FromPort, connection.ToPort)),
        Fluids.Select(ItemId.Of));

    public string ToJson() => JsonSerializer.Serialize(this, SimulationJson.Options);

    public static FlowNetworkDocument FromJson(string json)
    {
        FlowNetworkDocument document = JsonSerializer.Deserialize<FlowNetworkDocument>(json, SimulationJson.Options)
            ?? throw new JsonException("A flow network document can't be empty");
        return document.Version == CurrentVersion
            ? document
            : throw new NotSupportedException($"Flow network document version {document.Version} is not supported");
    }
}

public sealed record FlowConnectionDocument(string From, string To, int? FromPort = null, int? ToPort = null);

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

public sealed record ProviderDocument(string Id, string Item, double Offer) : FlowNodeDocument(Id)
{
    public override FlowNode ToNode() => new FlowProvider(BuildableId.Of(Id), ItemId.Of(Item), Offer);
}

/// <param name="BurnRate">For a drone port: its fuel per minute; null when that isn't known (BR-A16).</param>
public sealed record UnknownSourceDocument(
    string Id, IReadOnlyDictionary<string, double> Stock, Carries Carries, IReadOnlyList<string>? Burns = null, double? BurnRate = null)
    : FlowNodeDocument(Id)
{
    public override FlowNode ToNode() => new FlowUnknownSource(BuildableId.Of(Id), Items(Stock), Carries, Items(Burns), BurnRate);
}

/// <param name="Stocked">The inputs whose stock inside it lasts (BR-A17); null when none.</param>
public sealed record MachineDocument(
    string Id, IReadOnlyDictionary<string, double> Demand, IReadOnlyDictionary<string, double> Production, IReadOnlyList<string>? Stocked = null)
    : FlowNodeDocument(Id)
{
    public override FlowNode ToNode() => new FlowMachine(BuildableId.Of(Id), Items(Demand), Items(Production), Items(Stocked));
}

/// <param name="Capacity">Per minute; null when there's no limit.</param>
public sealed record ConduitDocument(string Id, double? Capacity, Carries Carries) : FlowNodeDocument(Id)
{
    public override FlowNode ToNode() => new FlowConduit(BuildableId.Of(Id), Capacity ?? double.PositiveInfinity, Carries);
}

public sealed record SortRuleDocument(int Output, SortRuleKind Kind, string? Item = null);

public sealed record SplitterDocument(string Id, Carries Carries, IReadOnlyList<SortRuleDocument>? Rules = null) : FlowNodeDocument(Id)
{
    public override FlowNode ToNode() => new FlowSplitter(BuildableId.Of(Id), Carries,
        Rules?.Select(rule => new SortRule(rule.Output, rule.Kind, rule.Item is null ? null : ItemId.Of(rule.Item))).ToList());
}

public sealed record MergerDocument(string Id, Carries Carries, IReadOnlyList<int>? Priorities = null) : FlowNodeDocument(Id)
{
    public override FlowNode ToNode() => new FlowMerger(BuildableId.Of(Id), Carries, Priorities);
}

/// <param name="BurnRate">For a drone port that loads: its fuel per minute; null when that isn't known (BR-A16).</param>
public sealed record SinkDocument(string Id, IReadOnlyList<string>? Burns = null, bool LeftoversOnly = false, double? BurnRate = null) : FlowNodeDocument(Id)
{
    public override FlowNode ToNode() => new FlowSink(BuildableId.Of(Id), Items(Burns), LeftoversOnly, BurnRate);
}

/// <summary>The JSON settings of the documents both services read and write: camelCase, enums as camelCase strings.</summary>
public static class SimulationJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
