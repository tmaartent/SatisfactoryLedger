namespace SatisfactoryLedger.World.Domain.Flow;

/// <summary>
/// The steady-state flow simulation (BR-A1 to BR-A10). It alternates two passes until nothing changes:
/// <list type="number">
/// <item><b>backward</b>: per connection, how much the receiving end can take (a belt's capacity, what a machine still
/// asks for, a merger's even share);</item>
/// <item><b>forward</b>: per connection, what the sending end sends (a provider's offer, a splitter's even split over
/// what its outputs take, a machine's output at its utilisation).</item>
/// </list>
/// Back-pressure comes from the backward pass, shortages from the forward pass. A loop just takes more sweeps.
/// Next to what flows, each connection carries what its sender <b>offers</b>: what it would send if the receiving end
/// took everything. That tells a starved machine (too little offered) from a blocked one (offered, but its output
/// can't leave), and keeps two inputs of one machine from waiting for each other.
/// Computed in <c>double</c>: results are approximations to <see cref="Tolerance"/> (BR-A10, ADR-0004).
/// </summary>
public static class SteadyState
{
    /// <summary>Results are meaningful to this many items per minute (BR-A10).</summary>
    public const double Tolerance = 0.0001;

    /// <summary>A source with an unknown rate is confirmed supplied when its stock lasts this long (BR-A9).</summary>
    public const double ConfirmedMinutes = 30;

    private const double Settle = 1e-9;
    private const int MaxSweeps = 50_000;

    public static FlowResult Of(FlowNetwork network) => new Solver(network).Run();

    private sealed class Solver
    {
        private readonly FlowNetwork _network;
        private readonly List<int>[] _in;
        private readonly List<int>[] _out;
        private readonly Dictionary<ItemId, double>[] _flow;
        private readonly Dictionary<ItemId, double>[] _offered;
        private readonly double[] _accept;
        private readonly int[] _order;
        private readonly HashSet<ItemId>[] _asked;
        private readonly Dictionary<BuildableId, MachineState> _machines = [];

        public Solver(FlowNetwork network)
        {
            _network = network;
            int nodes = network.Nodes.Count;
            int edges = network.Edges.Count;
            _in = [.. Enumerable.Range(0, nodes).Select(_ => new List<int>())];
            _out = [.. Enumerable.Range(0, nodes).Select(_ => new List<int>())];
            for (int edge = 0; edge < edges; edge++)
            {
                _out[network.Edges[edge].From].Add(edge);
                _in[network.Edges[edge].To].Add(edge);
            }

            _flow = [.. Enumerable.Range(0, edges).Select(_ => new Dictionary<ItemId, double>())];
            _offered = [.. Enumerable.Range(0, edges).Select(_ => new Dictionary<ItemId, double>())];
            _accept = [.. Enumerable.Repeat(double.PositiveInfinity, edges)];
            _order = ForwardOrder();
            _asked = [.. Enumerable.Range(0, nodes).Select(AskedItems)];
        }

        public FlowResult Run()
        {
            double[] previous = new double[_flow.Length];
            int sweep = 0;
            int calm = 0;
            while (sweep < MaxSweeps && calm < 2)
            {
                sweep++;
                Backward();
                Forward();

                double change = 0;
                for (int edge = 0; edge < _flow.Length; edge++)
                {
                    double total = Total(_flow[edge]);
                    change = Math.Max(change, Math.Abs(total - previous[edge]));
                    previous[edge] = total;
                }

                calm = change < Settle ? calm + 1 : 0;
            }

            return new FlowResult(
                _network,
                _flow,
                _machines,
                Bottlenecks(),
                Unused(),
                Unknown(),
                settled: calm >= 2,
                sweeps: sweep);
        }

        // ---- The backward pass: what each receiving end can take ---------------------------------------------

        private void Backward()
        {
            for (int i = _order.Length - 1; i >= 0; i--)
            {
                int node = _order[i];
                switch (_network.Nodes[node])
                {
                    case FlowSink:
                        _in[node].ForEach(edge => _accept[edge] = double.PositiveInfinity);
                        break;
                    case FlowConduit conduit:
                        ShareInputs(node, Math.Min(conduit.Capacity, AcceptedDownstream(node)));
                        break;
                    case FlowSplitter or FlowMerger:
                        ShareInputs(node, AcceptedDownstream(node));
                        break;
                    case FlowMachine machine:
                        AcceptForMachine(node, machine);
                        break;
                }
            }
        }

        private double AcceptedDownstream(int node) => _out[node].Sum(edge => _accept[edge]);

        /// <summary>A node that can pass on <paramref name="available"/>: its inputs share it like a merger's (BR-A5).</summary>
        private void ShareInputs(int node, double available)
        {
            List<int> inputs = _in[node];
            if (inputs.Count == 1)
            {
                _accept[inputs[0]] = available;
                return;
            }

            double[] taken = Waterfill(available, [.. inputs.Select(edge => Total(_offered[edge]))]);
            double total = taken.Sum();
            for (int k = 0; k < inputs.Count; k++)
            {
                // An input may take what the others leave, so an input that offers more later can grow into it.
                _accept[inputs[k]] = available - (total - taken[k]);
            }
        }

        /// <summary>
        /// BR-A1: of each input item, a machine takes what its scarcest other input and its outputs allow. A belt bringing
        /// an item it doesn't ask for clogs: nothing comes in over it.
        /// </summary>
        private void AcceptForMachine(int node, FlowMachine machine)
        {
            double outputs = OutputRoom(node, machine);
            Dictionary<ItemId, double> offered = Offered(node);

            foreach (int edge in _in[node])
            {
                List<ItemId> items = [.. _offered[edge].Where(entry => entry.Value > 0).Select(entry => entry.Key)];
                if (items.Count == 0)
                {
                    items = [.. machine.Demand.Keys];
                }

                if (items.Any(item => !machine.Demand.ContainsKey(item)))
                {
                    _accept[edge] = 0;
                    continue;
                }

                double accept = 0;
                foreach (ItemId item in items)
                {
                    double share = Math.Min(outputs, ScarcestOtherThan(machine, offered, item));
                    double fromOthers = _in[node].Where(other => other != edge).Sum(other => _flow[other].GetValueOrDefault(item));
                    accept += Math.Max(0, machine.Demand[item] * share - fromOthers);
                }

                _accept[edge] = accept;
            }
        }

        /// <summary>The share of 100% its outputs can take: all products share the machine's output connections.</summary>
        private double OutputRoom(int node, FlowMachine machine)
        {
            double making = machine.Production.Values.Sum();
            return making <= 0 ? 1 : Math.Min(1, AcceptedDownstream(node) / making);
        }

        private Dictionary<ItemId, double> Supplied(int node) => SumOver(_in[node], _flow);

        private Dictionary<ItemId, double> Offered(int node) => SumOver(_in[node], _offered);

        private static Dictionary<ItemId, double> SumOver(List<int> edges, Dictionary<ItemId, double>[] perEdge)
        {
            Dictionary<ItemId, double> sum = [];
            foreach (int edge in edges)
            {
                foreach ((ItemId item, double rate) in perEdge[edge])
                {
                    sum[item] = sum.GetValueOrDefault(item) + rate;
                }
            }

            return sum;
        }

        /// <summary>The lowest share of its demand that the machine's other inputs are offered: what it can use of this one.</summary>
        private static double ScarcestOtherThan(FlowMachine machine, Dictionary<ItemId, double> offered, ItemId except) =>
            machine.Demand.Where(demand => demand.Key != except && demand.Value > 0)
                .Select(demand => Math.Min(1, offered.GetValueOrDefault(demand.Key) / demand.Value))
                .Append(1)
                .Min();

        // ---- The forward pass: what each sending end sends ---------------------------------------------------

        private void Forward()
        {
            foreach (int node in _order)
            {
                switch (_network.Nodes[node])
                {
                    case FlowProvider provider:
                        Dictionary<ItemId, double> offer = new() { [provider.Item] = provider.Offer };
                        Send(node, offer, offer);
                        break;
                    case FlowUnknownSource:
                        SendAsked(node);
                        break;
                    case FlowConduit or FlowSplitter or FlowMerger:
                        Send(node, Supplied(node), Offered(node));
                        break;
                    case FlowMachine machine:
                        Run(node, machine);
                        break;
                }
            }
        }

        /// <summary>BR-A9: a source with an unknown rate sends what is asked of it, of the items asked downstream.</summary>
        private void SendAsked(int node)
        {
            HashSet<ItemId> asked = _asked[node];
            if (asked.Count == 0)
            {
                return;
            }

            double total = AcceptedDownstream(node);
            if (double.IsPositiveInfinity(total))
            {
                // Nothing downstream limits it yet; the next backward pass will.
                total = 0;
            }

            Dictionary<ItemId, double> sending = asked.ToDictionary(item => item, _ => total / asked.Count);
            Send(node, sending, sending);
        }

        /// <summary>BR-A1: a machine runs at the lowest of what its inputs supply and what its outputs take.</summary>
        private void Run(int node, FlowMachine machine)
        {
            if (machine.Demand.Count == 0 && machine.Production.Count == 0)
            {
                _machines[machine.Id] = new MachineState(machine.Id, 0, MachineLimit.NoRecipe, null);
                return;
            }

            (double inputs, _) = Scarcest(machine, Supplied(node));
            (double offeredShare, ItemId? scarcest) = Scarcest(machine, Offered(node));
            double outputs = OutputRoom(node, machine);
            double utilisation = Math.Min(inputs, outputs);

            // Back-pressure lowers what comes in as well, so the cause is read from what's offered:
            // offered enough but the outputs take less means blocked, too little offered means starved.
            (MachineLimit limit, ItemId? item) = utilisation >= 1 - Settle ? (MachineLimit.None, (ItemId?)null)
                : outputs < offeredShare - Settle ? (MachineLimit.OutputBlocked, machine.Production.Keys.First())
                : (MachineLimit.Starved, scarcest);
            _machines[machine.Id] = new MachineState(machine.Id, Math.Min(1, utilisation), limit, item);

            Send(node,
                machine.Production.ToDictionary(product => product.Key, product => product.Value * utilisation),
                machine.Production.ToDictionary(product => product.Key, product => product.Value * offeredShare));
        }

        /// <summary>The lowest share of its demand a machine gets of any input, and which item that is.</summary>
        private static (double Share, ItemId? Item) Scarcest(FlowMachine machine, Dictionary<ItemId, double> per) =>
            machine.Demand
                .Where(demand => demand.Value > 0)
                .Select(demand => (Share: Math.Min(1, per.GetValueOrDefault(demand.Key) / demand.Value), Item: (ItemId?)demand.Key))
                .Append((Share: 1.0, Item: null))
                .MinBy(entry => entry.Share);

        /// <summary>
        /// Sends <paramref name="items"/> over a node's outputs: evenly over what each output takes (BR-A4).
        /// <paramref name="offer"/> is what the node would send if nothing downstream held it back: each output is
        /// offered that, minus what the other outputs take.
        /// </summary>
        private void Send(int node, Dictionary<ItemId, double> items, Dictionary<ItemId, double> offer)
        {
            List<int> outputs = _out[node];
            if (outputs.Count == 0)
            {
                return;
            }

            double total = Total(items);
            double[] sent = Waterfill(total, [.. outputs.Select(edge => _accept[edge])]);
            double allSent = sent.Sum();
            for (int k = 0; k < outputs.Count; k++)
            {
                Dictionary<ItemId, double> flow = _flow[outputs[k]];
                flow.Clear();
                if (total > 0)
                {
                    foreach ((ItemId item, double rate) in items)
                    {
                        flow[item] = rate * sent[k] / total;
                    }
                }

                double offerTotal = Total(offer);
                double offeredHere = Math.Max(0, offerTotal - (allSent - sent[k]));
                Dictionary<ItemId, double> offered = _offered[outputs[k]];
                offered.Clear();
                if (offerTotal > 0)
                {
                    foreach ((ItemId item, double rate) in offer)
                    {
                        offered[item] = rate * offeredHere / offerTotal;
                    }
                }
            }
        }

        // ---- Results -----------------------------------------------------------------------------------------

        private List<Bottleneck> Bottlenecks()
        {
            List<Bottleneck> bottlenecks = [];
            for (int node = 0; node < _network.Nodes.Count; node++)
            {
                if (_network.Nodes[node] is not FlowConduit { Capacity: < double.PositiveInfinity } conduit)
                {
                    continue;
                }

                double flowing = _in[node].Sum(edge => Total(_flow[edge]));
                double offered = _in[node].Sum(edge => Total(_offered[edge]));
                // A line of ten Mk.1 segments is one bottleneck: only the first saturated segment of a chain counts.
                bool fedBySaturated = _in[node].Any(edge =>
                    _network.Nodes[_network.Edges[edge].From] is FlowConduit upstream && Saturated(_network.Edges[edge].From, upstream));
                if (flowing >= conduit.Capacity - Tolerance && offered - flowing > Tolerance && !fedBySaturated)
                {
                    bottlenecks.Add(new Bottleneck(conduit.Id, conduit.Capacity, flowing, offered - flowing));
                }
            }

            return bottlenecks;
        }

        private bool Saturated(int node, FlowConduit conduit) =>
            _in[node].Sum(edge => Total(_flow[edge])) >= conduit.Capacity - Tolerance;

        private List<UnusedSupply> Unused()
        {
            List<UnusedSupply> unused = [];
            for (int node = 0; node < _network.Nodes.Count; node++)
            {
                if (_network.Nodes[node] is FlowProvider provider)
                {
                    double delivered = _out[node].Sum(edge => Total(_flow[edge]));
                    if (provider.Offer - delivered > Tolerance)
                    {
                        unused.Add(new UnusedSupply(provider.Id, provider.Item, provider.Offer, delivered));
                    }
                }
            }

            return unused;
        }

        private List<UnknownSupply> Unknown()
        {
            List<UnknownSupply> unknown = [];
            for (int node = 0; node < _network.Nodes.Count; node++)
            {
                if (_network.Nodes[node] is not FlowUnknownSource source)
                {
                    continue;
                }

                Dictionary<ItemId, double> delivered = [];
                foreach (int edge in _out[node])
                {
                    foreach ((ItemId item, double rate) in _flow[edge].Where(entry => entry.Value > Tolerance))
                    {
                        delivered[item] = delivered.GetValueOrDefault(item) + rate;
                    }
                }

                if (delivered.Count == 0)
                {
                    continue;
                }

                double? minutes = delivered.Keys.All(source.Stock.ContainsKey)
                    ? delivered.Min(entry => source.Stock[entry.Key] / entry.Value)
                    : null;
                unknown.Add(new UnknownSupply(source.Id, delivered, minutes, minutes >= ConfirmedMinutes - Settle));
            }

            return unknown;
        }

        // ---- Helpers -----------------------------------------------------------------------------------------

        /// <summary>
        /// Divides <paramref name="total"/> evenly over receivers that take at most <paramref name="caps"/> each.
        /// What one can't take goes to the others. Like water filling containers of different heights.
        /// </summary>
        private static double[] Waterfill(double total, double[] caps)
        {
            double[] given = new double[caps.Length];
            HashSet<int> open = [.. Enumerable.Range(0, caps.Length).Where(k => caps[k] > 0)];
            double left = Math.Max(0, total);

            while (open.Count > 0 && left > Settle)
            {
                double share = left / open.Count;
                List<int> full = [.. open.Where(k => caps[k] - given[k] <= share)];
                if (full.Count == 0)
                {
                    foreach (int k in open)
                    {
                        given[k] += share;
                    }

                    break;
                }

                foreach (int k in full)
                {
                    left -= caps[k] - given[k];
                    given[k] = caps[k];
                    open.Remove(k);
                }
            }

            return given;
        }

        private static double Total(Dictionary<ItemId, double> items) => items.Values.Sum();

        /// <summary>Sources first, then downstream in the order items reach them; whatever no source reaches last.</summary>
        private int[] ForwardOrder()
        {
            List<int> order = [];
            bool[] seen = new bool[_network.Nodes.Count];
            Queue<int> next = new(Enumerable.Range(0, _network.Nodes.Count).Where(node => _in[node].Count == 0));
            foreach (int start in next)
            {
                seen[start] = true;
            }

            while (next.TryDequeue(out int node))
            {
                order.Add(node);
                foreach (int to in _out[node].Select(edge => _network.Edges[edge].To).Where(to => !seen[to]))
                {
                    seen[to] = true;
                    next.Enqueue(to);
                }
            }

            order.AddRange(Enumerable.Range(0, _network.Nodes.Count).Where(node => !seen[node]));
            return [.. order];
        }

        /// <summary>For a source with an unknown rate: the items the machines it feeds ask for, and the items it holds.</summary>
        private HashSet<ItemId> AskedItems(int node)
        {
            if (_network.Nodes[node] is not FlowUnknownSource source)
            {
                return [];
            }

            HashSet<ItemId> asked = [];
            HashSet<int> seen = [node];
            Queue<int> next = new([node]);
            while (next.TryDequeue(out int current))
            {
                foreach (int to in _out[current].Select(edge => _network.Edges[edge].To).Where(seen.Add))
                {
                    switch (_network.Nodes[to])
                    {
                        case FlowMachine machine:
                            asked.UnionWith(machine.Demand.Keys);
                            break;
                        case FlowConduit or FlowSplitter or FlowMerger:
                            next.Enqueue(to);
                            break;
                    }
                }
            }

            // What it holds narrows it down when it's known; a station holding nothing right now still supplies what's asked.
            HashSet<ItemId> held = [.. source.Stock.Where(stock => stock.Value > 0).Select(stock => stock.Key)];
            return held.Count > 0 && held.Overlaps(asked) ? [.. asked.Intersect(held)] : asked;
        }
    }
}
