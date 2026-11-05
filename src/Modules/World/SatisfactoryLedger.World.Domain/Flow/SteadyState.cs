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
/// A connection carries solids or fluids, as its belt or pipe does: solids and fluids are divided separately.
/// Computed in <c>double</c>: results are approximations to <see cref="Tolerance"/> (BR-A10, ADR-0004).
/// Each connected part of the network is simulated on its own: it settles at its own pace, and one that doesn't
/// settle within <see cref="MaxSweeps"/> is reported as approximate without holding up the rest.
/// </summary>
public static class SteadyState
{
    /// <summary>Results are meaningful to this many items per minute (BR-A10).</summary>
    public const double Tolerance = 0.0001;

    /// <summary>A source with an unknown rate is confirmed supplied when its stock lasts this long (BR-A9).</summary>
    public const double ConfirmedMinutes = 30;

    /// <summary>The most sweeps one part gets. A part that hasn't settled by then is approximate.</summary>
    public const int MaxSweeps = 2_000;

    /// <summary>For comparing doubles inside the solver: a difference smaller than this is no difference.</summary>
    private const double Settle = 1e-9;

    /// <summary>Solids, then fluids: <c>true</c> means fluid.</summary>
    private static readonly bool[] Forms = [false, true];

    public static FlowResult Of(FlowNetwork network, int maxSweeps = MaxSweeps)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSweeps);

        var flows = new IReadOnlyDictionary<ItemId, double>[network.Edges.Count];
        Dictionary<BuildableId, MachineState> machines = [];
        List<Bottleneck> bottlenecks = [];
        List<UnusedSupply> unused = [];
        List<UnknownSupply> unknown = [];
        List<FlowPart> parts = [];

        foreach ((List<int> nodes, List<int> edges) in PartsOf(network))
        {
            var part = new FlowNetwork(
                nodes.Select(node => network.Nodes[node]),
                edges.Select(edge => new Connection(network.Nodes[network.Edges[edge].From].Id, network.Nodes[network.Edges[edge].To].Id)),
                network.Fluids);
            PartResult result = new Solver(part).Run(maxSweeps);

            for (int k = 0; k < edges.Count; k++)
            {
                flows[edges[k]] = result.Flows[k];
            }

            foreach ((BuildableId id, MachineState state) in result.Machines)
            {
                machines[id] = state;
            }

            bottlenecks.AddRange(result.Bottlenecks);
            unused.AddRange(result.Unused);
            unknown.AddRange(result.Unknown);
            parts.Add(new FlowPart([.. nodes.Select(node => network.Nodes[node].Id)], result.Settled, result.Sweeps));
        }

        return new FlowResult(network, flows, machines, bottlenecks, unused, unknown, parts);
    }

    /// <summary>The connected parts of a network, ignoring direction: per part its nodes and its connections.</summary>
    private static List<(List<int> Nodes, List<int> Edges)> PartsOf(FlowNetwork network)
    {
        int[] root = [.. Enumerable.Range(0, network.Nodes.Count)];
        int Find(int node)
        {
            while (root[node] != node)
            {
                root[node] = root[root[node]];
                node = root[node];
            }

            return node;
        }

        foreach ((int from, int to) in network.Edges)
        {
            root[Find(from)] = Find(to);
        }

        Dictionary<int, (List<int> Nodes, List<int> Edges)> parts = [];
        for (int node = 0; node < network.Nodes.Count; node++)
        {
            int part = Find(node);
            if (!parts.TryGetValue(part, out (List<int> Nodes, List<int> Edges) found))
            {
                found = ([], []);
                parts[part] = found;
            }

            found.Nodes.Add(node);
        }

        for (int edge = 0; edge < network.Edges.Count; edge++)
        {
            parts[Find(network.Edges[edge].From)].Edges.Add(edge);
        }

        return [.. parts.Values];
    }

    /// <summary>What one part's simulation found. Connections are numbered as in that part's own network.</summary>
    private sealed record PartResult(
        IReadOnlyList<Dictionary<ItemId, double>> Flows,
        IReadOnlyDictionary<BuildableId, MachineState> Machines,
        List<Bottleneck> Bottlenecks,
        List<UnusedSupply> Unused,
        List<UnknownSupply> Unknown,
        bool Settled,
        int Sweeps);

    private sealed class Solver
    {
        private readonly FlowNetwork _network;
        private readonly List<int>[] _in;
        private readonly List<int>[] _out;
        private readonly Dictionary<ItemId, double>[] _flow;
        private readonly Dictionary<ItemId, double>[] _offered;
        private readonly double[] _accept;
        private readonly Carries[] _carries;
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
            _carries = [.. network.Edges.Select(edge => Both(CarriesOf(edge.From), CarriesOf(edge.To)))];
            _order = ForwardOrder();
            _asked = [.. Enumerable.Range(0, nodes).Select(AskedItems)];
        }

        public PartResult Run(int maxSweeps)
        {
            double[] previous = new double[_flow.Length];
            int sweep = 0;
            int calm = 0;
            while (sweep < maxSweeps && calm < 2)
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

                // BR-A10: settled when no flow changes by more than the tolerance, twice in a row.
                calm = change < Tolerance ? calm + 1 : 0;
            }

            return new PartResult(_flow, _machines, Bottlenecks(), Unused(), Unknown(), Settled: calm >= 2, sweep);
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
            (double outputs, _) = OutputRoom(node, machine);
            Dictionary<ItemId, double> offered = Offered(node);

            foreach (int edge in _in[node])
            {
                List<ItemId> items = [.. _offered[edge].Where(entry => entry.Value > 0).Select(entry => entry.Key)];
                if (items.Count == 0)
                {
                    items = [.. machine.Demand.Keys.Where(item => CanCarry(edge, item))];
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

        /// <summary>
        /// The share of 100% its outputs can take, and the product that limits it. Solid products share the outputs
        /// that carry solids, fluid products the ones that carry fluids; the tighter of the two decides.
        /// </summary>
        private (double Room, ItemId? Item) OutputRoom(int node, FlowMachine machine)
        {
            (double Room, ItemId? Item) tightest = (1, null);
            foreach (bool fluid in Forms)
            {
                List<ItemId> products = [.. machine.Production.Where(product => product.Value > 0 && IsFluid(product.Key) == fluid).Select(product => product.Key)];
                double making = products.Sum(product => machine.Production[product]);
                if (making <= 0)
                {
                    continue;
                }

                double room = Math.Min(1, _out[node].Where(edge => CanCarry(edge, fluid)).Sum(edge => _accept[edge]) / making);
                if (room < tightest.Room)
                {
                    tightest = (room, products[0]);
                }
            }

            return tightest;
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
            (double outputs, ItemId? blocked) = OutputRoom(node, machine);
            double utilisation = Math.Min(inputs, outputs);

            // Back-pressure lowers what comes in as well, so the cause is read from what's offered:
            // offered enough but the outputs take less means blocked, too little offered means starved.
            (MachineLimit limit, ItemId? item) = utilisation >= 1 - Settle ? (MachineLimit.None, (ItemId?)null)
                : outputs < offeredShare - Settle ? (MachineLimit.OutputBlocked, blocked ?? machine.Production.Keys.First())
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
        /// Sends <paramref name="items"/> over a node's outputs: evenly over what each output takes (BR-A4), solids
        /// over the outputs that carry solids and fluids over the ones that carry fluids.
        /// <paramref name="offer"/> is what the node would send if nothing downstream held it back: each output is
        /// offered that, minus what the other outputs take.
        /// </summary>
        private void Send(int node, Dictionary<ItemId, double> items, Dictionary<ItemId, double> offer)
        {
            List<int> outputs = _out[node];
            foreach (int edge in outputs)
            {
                _flow[edge].Clear();
                _offered[edge].Clear();
            }

            // What each output can still take: an output that carries anything is shared by solids and fluids.
            double[] room = [.. outputs.Select(edge => _accept[edge])];
            foreach (bool fluid in Forms)
            {
                Dictionary<ItemId, double> sending = OfForm(items, fluid);
                Dictionary<ItemId, double> offering = OfForm(offer, fluid);
                if (sending.Count == 0 && offering.Count == 0)
                {
                    continue;
                }

                bool[] carries = [.. outputs.Select(edge => CanCarry(edge, fluid))];
                double total = Total(sending);
                double[] sent = Waterfill(total, [.. Enumerable.Range(0, outputs.Count).Select(k => carries[k] ? room[k] : 0)]);
                double allSent = sent.Sum();
                double offerTotal = Total(offering);
                for (int k = 0; k < outputs.Count; k++)
                {
                    if (!carries[k])
                    {
                        continue;
                    }

                    if (total > 0)
                    {
                        foreach ((ItemId item, double rate) in sending)
                        {
                            _flow[outputs[k]][item] = rate * sent[k] / total;
                        }
                    }

                    double offeredHere = Math.Max(0, offerTotal - (allSent - sent[k]));
                    if (offerTotal > 0)
                    {
                        foreach ((ItemId item, double rate) in offering)
                        {
                            _offered[outputs[k]][item] = rate * offeredHere / offerTotal;
                        }
                    }

                    room[k] -= sent[k];
                }
            }
        }

        private Dictionary<ItemId, double> OfForm(Dictionary<ItemId, double> items, bool fluid) =>
            items.Where(entry => IsFluid(entry.Key) == fluid).ToDictionary(entry => entry.Key, entry => entry.Value);

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

        private bool IsFluid(ItemId item) => _network.Fluids.Contains(item);

        private bool CanCarry(int edge, ItemId item) => CanCarry(edge, IsFluid(item));

        private bool CanCarry(int edge, bool fluid) => _carries[edge] switch
        {
            Carries.Solids => !fluid,
            Carries.Fluids => fluid,
            _ => true,
        };

        private Carries CarriesOf(int node) => _network.Nodes[node] switch
        {
            FlowConduit conduit => conduit.Carries,
            FlowSplitter splitter => splitter.Carries,
            FlowMerger merger => merger.Carries,
            FlowUnknownSource source => source.Carries,
            FlowProvider provider => IsFluid(provider.Item) ? Carries.Fluids : Carries.Solids,
            _ => Carries.Anything,
        };

        /// <summary>A connection carries what both its ends allow. A machine or sink allows anything: its line decides.</summary>
        private static Carries Both(Carries from, Carries to) => from == Carries.Anything ? to : from;

        /// <summary>
        /// A node comes after everything that feeds it (a topological order), so one forward pass carries a change all
        /// the way down, and one backward pass all the way up. A loop has no such order: when only loops are left, the
        /// first node of one goes next, and the rest follows from it.
        /// </summary>
        private int[] ForwardOrder()
        {
            int count = _network.Nodes.Count;
            int[] waiting = [.. Enumerable.Range(0, count).Select(node => _in[node].Count)];
            bool[] placed = new bool[count];
            List<int> order = new(count);
            Queue<int> ready = new(Enumerable.Range(0, count).Where(node => waiting[node] == 0));
            int nextUnplaced = 0;

            while (order.Count < count)
            {
                if (ready.Count == 0)
                {
                    // Only loops are left: break one open at the first node not placed yet.
                    while (placed[nextUnplaced])
                    {
                        nextUnplaced++;
                    }

                    ready.Enqueue(nextUnplaced);
                }

                int node = ready.Dequeue();
                if (placed[node])
                {
                    continue;
                }

                placed[node] = true;
                order.Add(node);
                foreach (int to in _out[node].Select(edge => _network.Edges[edge].To))
                {
                    if (--waiting[to] == 0 && !placed[to])
                    {
                        ready.Enqueue(to);
                    }
                }
            }

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
                foreach (int edge in _out[current].Where(edge => seen.Add(_network.Edges[edge].To)))
                {
                    int to = _network.Edges[edge].To;
                    switch (_network.Nodes[to])
                    {
                        case FlowMachine machine:
                            // Only what its line can bring: a belt into a packager brings canisters, not water.
                            asked.UnionWith(machine.Demand.Keys.Where(item => CanCarry(edge, item)));
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
