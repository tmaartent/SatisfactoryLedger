using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.Domain.Flow.Objects;

namespace SatisfactoryLedger.World.Domain.Tests;

/// <summary>Builds a small flow network by hand: nodes by name with their rates per minute, then connections.</summary>
internal sealed class TestFlow
{
    private readonly List<FlowNode> _nodes = [];
    private readonly List<Connection> _connections = [];
    private readonly HashSet<ItemId> _fluids = [];

    public static BuildableId Id(string name) => BuildableId.Of(name);

    public TestFlow Provider(string id, ItemId item, double offer) => Add(new FlowProvider(Id(id), item, offer));

    public TestFlow UnknownSource(string id, params (ItemId Item, double Count)[] stock) =>
        Add(new FlowUnknownSource(Id(id), stock.ToDictionary(entry => entry.Item, entry => entry.Count)));

    /// <summary>A station that holds nothing, assumed to carry <paramref name="cargo"/>: what its neighbours hold (BR-A9).</summary>
    public TestFlow EmptyStation(string id, params ItemId[] cargo) =>
        Add(new FlowUnknownSource(Id(id), new Dictionary<ItemId, double>(), Cargo: cargo.ToHashSet()));

    public TestFlow Machine(string id, (ItemId Item, double Rate)[] demand, (ItemId Item, double Rate)[] production) =>
        Add(new FlowMachine(Id(id), demand.ToDictionary(e => e.Item, e => e.Rate), production.ToDictionary(e => e.Item, e => e.Rate)));

    /// <summary>A machine whose stock of <paramref name="stocked"/> lasts at least 30 minutes (BR-A17).</summary>
    public TestFlow MachineOnStock(string id, (ItemId Item, double Rate)[] demand, (ItemId Item, double Rate)[] production, params ItemId[] stocked) =>
        Add(new FlowMachine(Id(id), demand.ToDictionary(e => e.Item, e => e.Rate), production.ToDictionary(e => e.Item, e => e.Rate), stocked.ToHashSet()));

    /// <summary>A machine with one input and one output.</summary>
    public TestFlow Machine(string id, ItemId input, double asks, ItemId output, double makes) =>
        Machine(id, [(input, asks)], [(output, makes)]);

    /// <summary>Marks an item as a fluid: it goes over pipes, not belts. Every other item is a solid.</summary>
    public TestFlow Fluid(ItemId item)
    {
        _fluids.Add(item);
        return this;
    }

    public TestFlow Belt(string id, double capacity = 1200) => Add(new FlowConduit(Id(id), capacity, Carries.Solids));

    public TestFlow Pipe(string id, double capacity = 600) => Add(new FlowConduit(Id(id), capacity, Carries.Fluids));

    /// <summary>A pipe network as one hub (BR-A6): it merges what feeds it and shares it over what it feeds.</summary>
    public TestFlow PipeNetwork(string id) => Add(new FlowSplitter(Id(id), Carries.Fluids));

    public TestFlow Splitter(string id) => Add(new FlowSplitter(Id(id), Carries.Solids));

    /// <summary>A smart splitter with its rules per output (BR-A4). Connect its outputs with <see cref="ConnectPort"/>.</summary>
    public TestFlow SmartSplitter(string id, params SortRule[] rules) => Add(new FlowSplitter(Id(id), Carries.Solids, rules));

    public TestFlow Merger(string id) => Add(new FlowMerger(Id(id), Carries.Solids));

    /// <summary>A priority merger with its priority per input (BR-A5): a higher number goes first. Connect with <see cref="ConnectInto"/>.</summary>
    public TestFlow PriorityMerger(string id, params int[] priorities) => Add(new FlowMerger(Id(id), Carries.Solids, priorities));

    public TestFlow Sink(string id) => Add(new FlowSink(Id(id)));

    /// <summary>Something with an inventory at the end of a line (BR-A7): a container, a station that loads, an uploader.</summary>
    public TestFlow Inventory(string id, Inventory inventory, bool leftoversOnly = false) =>
        Add(new FlowSink(Id(id), LeftoversOnly: leftoversOnly, Inventory: inventory));

    /// <summary>A dimensional depot uploader: takes only what the line's other consumers leave (BR-A8).</summary>
    public TestFlow Uploader(string id) => Add(new FlowSink(Id(id), LeftoversOnly: true));

    /// <summary>A station that unloads to its outputs and burns <paramref name="fuels"/> that come in on its inputs.</summary>
    public TestFlow StationBurning(string id, params ItemId[] fuels) =>
        Add(new FlowUnknownSource(Id(id), new Dictionary<ItemId, double>(), Carries.Anything, fuels.ToHashSet()));

    /// <summary>A drone port that unloads to its outputs and burns <paramref name="perMinute"/> of <paramref name="fuel"/> (BR-A16).</summary>
    public TestFlow StationBurning(string id, ItemId fuel, double perMinute) =>
        Add(new FlowUnknownSource(Id(id), new Dictionary<ItemId, double>(), Carries.Anything, new HashSet<ItemId> { fuel }, perMinute));

    /// <summary>A drone port that only loads, and burns <paramref name="perMinute"/> of <paramref name="fuel"/> (BR-A16).</summary>
    public TestFlow LoadingStationBurning(string id, ItemId fuel, double perMinute) =>
        Add(new FlowSink(Id(id), new HashSet<ItemId> { fuel }, BurnRate: perMinute));

    /// <summary>A station that only loads, and burns <paramref name="fuels"/> that come in on its inputs.</summary>
    public TestFlow LoadingStationBurning(string id, params ItemId[] fuels) => Add(new FlowSink(Id(id), fuels.ToHashSet()));

    public TestFlow Connect(params string[] chain)
    {
        for (int i = 1; i < chain.Length; i++)
        {
            _connections.Add(new Connection(Id(chain[i - 1]), Id(chain[i])));
        }

        return this;
    }

    /// <summary>Connects output <paramref name="port"/> of a splitter to the next buildable.</summary>
    public TestFlow ConnectPort(string from, int port, string to)
    {
        _connections.Add(new Connection(Id(from), Id(to), FromPort: port));
        return this;
    }

    /// <summary>Connects a buildable to input <paramref name="port"/> of a merger.</summary>
    public TestFlow ConnectInto(string from, string to, int port)
    {
        _connections.Add(new Connection(Id(from), Id(to), ToPort: port));
        return this;
    }

    public FlowResult Run(int maxSweeps = SteadyState.MaxSweeps) => SteadyState.Of(Network(), maxSweeps);

    /// <summary>The network as built: to start the sweeps from a running state (ADR-0026).</summary>
    public FlowNetwork Network() => new(_nodes, _connections, _fluids);

    private TestFlow Add(FlowNode node)
    {
        _nodes.Add(node);
        return this;
    }
}
