using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow.Objects;

namespace SatisfactoryLedger.World.Simulation;

/// <summary>
/// Per connection, the items that can reach it: from where they enter (a provider's item, a machine's products, what a
/// source of unknown rate is asked for), along logistics, as far as each connection carries their form (BR-A3) and the
/// buildable at its end takes them. The linear programme has a flow variable for these only.
/// </summary>
internal sealed class ItemsOnConnections
{
    private readonly FlowNetwork _network;
    private readonly List<int>[] _out;
    private readonly Carries[] _carries;
    private readonly HashSet<ItemId>[] _items;
    private readonly Dictionary<int, double> _asked = [];
    private readonly Queue<(int Edge, ItemId Item)> _toPass = new();

    private ItemsOnConnections(FlowNetwork network)
    {
        _network = network;
        _out = [.. Enumerable.Range(0, network.Nodes.Count).Select(_ => new List<int>())];
        for (int edge = 0; edge < network.Edges.Count; edge++)
        {
            _out[network.Edges[edge].From].Add(edge);
        }

        _carries = [.. network.Edges.Select(edge => Both(CarriesOf(edge.From), CarriesOf(edge.To)))];
        _items = [.. network.Edges.Select(_ => new HashSet<ItemId>())];
    }

    /// <summary>Per connection, in the network's order, the items that can flow over it.</summary>
    public IReadOnlyList<IReadOnlySet<ItemId>> Items => _items;

    /// <summary>Per source of unknown rate, by its position: how much the machines it reaches ask a minute of what it sends (BR-A9).</summary>
    public IReadOnlyDictionary<int, double> Asked => _asked;

    public static ItemsOnConnections Of(FlowNetwork network)
    {
        var items = new ItemsOnConnections(network);
        items.Enter();
        items.Pass();
        return items;
    }

    private void Enter()
    {
        for (int node = 0; node < _network.Nodes.Count; node++)
        {
            IEnumerable<ItemId> entering = _network.Nodes[node] switch
            {
                FlowProvider provider => [provider.Item],
                FlowMachine machine => machine.Production.Where(product => product.Value > 0).Select(product => product.Key),
                FlowUnknownSource source => AskedOf(node, source),
                _ => [],
            };

            foreach (ItemId item in entering)
            {
                _out[node].ForEach(edge => Offer(edge, item));
            }
        }
    }

    /// <summary>Logistics passes on everything that reaches it.</summary>
    private void Pass()
    {
        while (_toPass.TryDequeue(out (int Edge, ItemId Item) arrived))
        {
            int at = _network.Edges[arrived.Edge].To;
            switch (_network.Nodes[at])
            {
                // A smart or programmable splitter sends an item only to the outputs its rules name (BR-A4).
                case FlowSplitter { Rules.Count: > 0 } sorting:
                    var rules = new SortRules(_network, sorting);
                    _out[at].Where(edge => rules.Takes(edge, arrived.Item) || rules.Overflows(edge, arrived.Item))
                        .ToList().ForEach(edge => Offer(edge, arrived.Item));
                    break;
                case FlowConduit or FlowSplitter or FlowMerger:
                    _out[at].ForEach(edge => Offer(edge, arrived.Item));
                    break;
            }
        }
    }

    private void Offer(int edge, ItemId item)
    {
        bool takes = _network.Nodes[_network.Edges[edge].To] is not FlowMachine machine || machine.Demand.ContainsKey(item);
        if (CanCarry(edge, item) && takes && _items[edge].Add(item))
        {
            _toPass.Enqueue((edge, item));
        }
    }

    /// <summary>
    /// BR-A9: what a source of unknown rate is asked for: the inputs of the machines it reaches through logistics, as
    /// far as the line into each carries them, narrowed to what it holds when it holds any of them.
    /// </summary>
    private HashSet<ItemId> AskedOf(int node, FlowUnknownSource source)
    {
        Dictionary<ItemId, double> asked = [];
        HashSet<int> seen = [node];
        Queue<int> next = new([node]);
        while (next.TryDequeue(out int current))
        {
            foreach (int edge in _out[current].Where(edge => seen.Add(_network.Edges[edge].To)))
            {
                switch (_network.Nodes[_network.Edges[edge].To])
                {
                    case FlowMachine machine:
                        foreach ((ItemId item, double rate) in machine.Demand.Where(demand => CanCarry(edge, demand.Key)))
                        {
                            asked[item] = asked.GetValueOrDefault(item) + rate;
                        }

                        break;
                    // BR-A16: a drone port burning a known rate asks for its fuel, like a generator. One whose rate
                    // isn't known asks for nothing: it takes only what's left.
                    case FlowSink { BurnRate: { } burnRate, Burns: { Count: > 0 } fuels }:
                        Ask(fuels, burnRate, edge);
                        break;
                    case FlowUnknownSource { BurnRate: { } burnRate, Burns: { Count: > 0 } fuels }:
                        Ask(fuels, burnRate, edge);

                        break;
                    case FlowConduit or FlowSplitter or FlowMerger:
                        next.Enqueue(_network.Edges[edge].To);
                        break;
                }
            }
        }

        void Ask(IEnumerable<ItemId> items, double rate, int edge)
        {
            foreach (ItemId item in items.Where(item => CanCarry(edge, item)))
            {
                asked[item] = asked.GetValueOrDefault(item) + rate;
            }
        }

        HashSet<ItemId> held = [.. source.Stock.Where(stock => stock.Value > 0).Select(stock => stock.Key)];
        HashSet<ItemId> sends = held.Overlaps(asked.Keys) ? [.. asked.Keys.Intersect(held)] : [.. asked.Keys];
        _asked[node] = sends.Sum(item => asked[item]);
        return sends;
    }

    private bool CanCarry(int edge, ItemId item) => _carries[edge] switch
    {
        Carries.Solids => !_network.Fluids.Contains(item),
        Carries.Fluids => _network.Fluids.Contains(item),
        _ => true,
    };

    private Carries CarriesOf(int node) => _network.Nodes[node] switch
    {
        FlowConduit conduit => conduit.Carries,
        FlowSplitter splitter => splitter.Carries,
        FlowMerger merger => merger.Carries,
        FlowUnknownSource source => source.Carries,
        FlowProvider provider => _network.Fluids.Contains(provider.Item) ? Carries.Fluids : Carries.Solids,
        _ => Carries.Anything,
    };

    /// <summary>A connection carries what both its ends allow. A machine or sink allows anything: its line decides.</summary>
    private static Carries Both(Carries from, Carries to) => from == Carries.Anything ? to : from;
}
