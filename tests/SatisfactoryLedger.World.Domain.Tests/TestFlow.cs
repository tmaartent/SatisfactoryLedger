using SatisfactoryLedger.World.Domain.Flow;

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

    public TestFlow Machine(string id, (ItemId Item, double Rate)[] demand, (ItemId Item, double Rate)[] production) =>
        Add(new FlowMachine(Id(id), demand.ToDictionary(e => e.Item, e => e.Rate), production.ToDictionary(e => e.Item, e => e.Rate)));

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

    public TestFlow Splitter(string id) => Add(new FlowSplitter(Id(id), Carries.Solids));

    public TestFlow Merger(string id) => Add(new FlowMerger(Id(id), Carries.Solids));

    public TestFlow Sink(string id) => Add(new FlowSink(Id(id)));

    public TestFlow Connect(params string[] chain)
    {
        for (int i = 1; i < chain.Length; i++)
        {
            _connections.Add(new Connection(Id(chain[i - 1]), Id(chain[i])));
        }

        return this;
    }

    public FlowResult Run(int maxSweeps = SteadyState.MaxSweeps) =>
        SteadyState.Of(new FlowNetwork(_nodes, _connections, _fluids), maxSweeps);

    private TestFlow Add(FlowNode node)
    {
        _nodes.Add(node);
        return this;
    }
}
