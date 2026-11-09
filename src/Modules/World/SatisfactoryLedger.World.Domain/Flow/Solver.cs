using SatisfactoryLedger.World.Domain.Flow.Objects;

namespace SatisfactoryLedger.World.Domain.Flow;

internal sealed class Solver
{
    private readonly FlowNetwork _network;
    private readonly List<int>[] _in;
    private readonly List<int>[] _out;
    private readonly Dictionary<ItemId, double>[] _flow;
    private readonly Dictionary<ItemId, double>[] _offered;
    private readonly double[] _accept;
    private readonly double[] _spare;
    private readonly Carries[] _carries;
    private readonly int[] _order;
    private readonly Dictionary<ItemId, double>[] _asked;
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
        _spare = new double[edges];
        _carries = [.. network.Edges.Select(edge => Both(CarriesOf(edge.From), CarriesOf(edge.To)))];
        _order = ForwardOrder();
        _asked = [.. Enumerable.Range(0, nodes).Select(AskedItems)];
        PrimeLoops();
    }

    /// <summary>
    /// BR-A10: machines in a loop wait on each other, so "all at zero" is a steady state too, and the sweeps, starting
    /// from nothing, would find it. In the game a loop that was started once keeps itself going (decided 2026-11-07).
    /// So before the first sweep, a machine in a loop is offered all it asks, and every link inside the loop what the
    /// loop's machines make at 100%, as if it were running. The sweeps then find what it really runs at: a loop that can't keep itself going still winds down.
    /// Everything outside loops starts from zero, as before.
    /// </summary>
    private void PrimeLoops()
    {
        int[] loopOf = Loops();
        for (int node = 0; node < _network.Nodes.Count; node++)
        {
            if (loopOf[node] < 0 || _network.Nodes[node] is not FlowMachine machine)
            {
                continue;
            }

            // As if fully supplied: every input is offered what it asks. A machine that is offered nothing of one input
            // in the first sweep would take nothing from the loop, and the loop's links would stay shut.
            foreach (int edge in _in[node])
            {
                foreach ((ItemId item, double demand) in machine.Demand.Where(demand =>
                             demand.Value > 0 && CanCarry(edge, demand.Key)))
                {
                    _offered[edge][item] = Math.Max(_offered[edge].GetValueOrDefault(item), demand);
                }
            }

            // Along the links its products take inside the loop, up to and including the link into the next machine.
            foreach ((ItemId item, double rate) in machine.Production.Where(product => product.Value > 0))
            {
                HashSet<int> seen = [node];
                Queue<int> next = new([node]);
                while (next.TryDequeue(out int current))
                {
                    foreach (int edge in _out[current])
                    {
                        int to = _network.Edges[edge].To;
                        if (loopOf[to] != loopOf[node] || !CanCarry(edge, item))
                        {
                            continue;
                        }

                        // Flowing too: a loop nothing feeds from outside would otherwise have nothing to start on.
                        _offered[edge][item] = Math.Max(_offered[edge].GetValueOrDefault(item), rate);
                        _flow[edge][item] = Math.Max(_flow[edge].GetValueOrDefault(item), rate);
                        if (_network.Nodes[to] is not FlowMachine && seen.Add(to))
                        {
                            next.Enqueue(to);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Per node, the loop it's in: its strongly connected part of the network, when that has more than one node and a
    /// machine in it; -1 when it's in none. Tarjan's algorithm, without recursion: a big world has long lines.
    /// </summary>
    private int[] Loops()
    {
        int count = _network.Nodes.Count;
        int[] index = [.. Enumerable.Repeat(-1, count)];
        int[] low = new int[count];
        bool[] onStack = new bool[count];
        int[] loopOf = [.. Enumerable.Repeat(-1, count)];
        Stack<int> stack = new();
        int counter = 0;
        int loops = 0;

        for (int start = 0; start < count; start++)
        {
            if (index[start] >= 0)
            {
                continue;
            }

            // Each frame: a node, and how far through its outgoing links it is.
            Stack<(int Node, int Next)> frames = new();
            frames.Push((start, 0));
            index[start] = low[start] = counter++;
            stack.Push(start);
            onStack[start] = true;
            while (frames.Count > 0)
            {
                (int node, int next) = frames.Pop();
                if (next < _out[node].Count)
                {
                    frames.Push((node, next + 1));
                    int to = _network.Edges[_out[node][next]].To;
                    if (index[to] < 0)
                    {
                        index[to] = low[to] = counter++;
                        stack.Push(to);
                        onStack[to] = true;
                        frames.Push((to, 0));
                    }
                    else if (onStack[to])
                    {
                        low[node] = Math.Min(low[node], index[to]);
                    }

                    continue;
                }

                if (frames.TryPeek(out (int Node, int Next) parent))
                {
                    low[parent.Node] = Math.Min(low[parent.Node], low[node]);
                }

                if (low[node] != index[node])
                {
                    continue;
                }

                List<int> part = [];
                int member;
                do
                {
                    member = stack.Pop();
                    onStack[member] = false;
                    part.Add(member);
                } while (member != node);

                if (part.Count > 1 && part.Any(id => _network.Nodes[id] is FlowMachine))
                {
                    part.ForEach(id => loopOf[id] = loops);
                    loops++;
                }
            }
        }

        return loopOf;
    }


    public PartResult Run(int maxSweeps)
    {
        double[] previous = new double[_flow.Length];
        int sweep = 0;
        int calm = 0;
        while (sweep < maxSweeps && calm < 2)
        {
            sweep++;
            double[] accepted = (double[])_accept.Clone();
            double[] spared = (double[])_spare.Clone();
            Backward();
            if (sweep > SteadyState.DampAfter)
            {
                Damp(_accept, accepted);
                Damp(_spare, spared);
            }

            Forward();

            double change = 0;
            for (int edge = 0; edge < _flow.Length; edge++)
            {
                double total = Total(_flow[edge]);
                change = Math.Max(change, Math.Abs(total - previous[edge]));
                previous[edge] = total;
            }

            // BR-A10: settled when no flow changes by more than the tolerance, twice in a row.
            calm = change < SteadyState.Tolerance ? calm + 1 : 0;
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
                case FlowSink sink:
                    TakeEverything(node, sink.Burns, sink.LeftoversOnly, sink.BurnRate);
                    break;
                // A station takes everything loaded onto it, like a consumer, while it unloads to its outputs (BR-A8, BR-A9).
                case FlowUnknownSource source:
                    TakeEverything(node, source.Burns, burnRate: source.BurnRate);
                    break;
                case FlowConduit conduit:
                    double firm = Math.Min(conduit.Capacity, AcceptedDownstream(node));
                    ShareInputs(node, firm, Math.Min(Left(conduit.Capacity, firm), SpareDownstream(node)));
                    break;
                case FlowSplitter { Rules.Count: > 0 } sorting:
                    List<int> receiving =
                    [
                        .. _out[node].Where(edge => RulesAt(sorting, edge).Any(rule => rule.Kind != SortRuleKind.None))
                    ];
                    ShareInputs(node, receiving.Sum(edge => _accept[edge]), receiving.Sum(edge => _spare[edge]));
                    break;
                case FlowSplitter or FlowMerger:
                    ShareInputs(node, AcceptedDownstream(node), SpareDownstream(node));
                    break;
                case FlowMachine machine:
                    AcceptForMachine(node, machine);
                    _in[node].ForEach(edge => _spare[edge] = 0);
                    break;
            }
        }
    }

    /// <summary>
    /// A part that hasn't settled after <see cref="DampAfter"/> sweeps may be flip-flopping between two states: from
    /// then on, what a connection accepts moves only halfway towards its new value each sweep.
    /// </summary>
    private static void Damp(double[] now, double[] before)
    {
        for (int edge = 0; edge < now.Length; edge++)
        {
            if (!double.IsPositiveInfinity(now[edge]) && !double.IsPositiveInfinity(before[edge]))
            {
                now[edge] = (now[edge] + before[edge]) / 2;
            }
        }
    }

    private double AcceptedDownstream(int node) => _out[node].Sum(edge => _accept[edge]);

    private double SpareDownstream(int node) => _out[node].Sum(edge => _spare[edge]);

    /// <summary>What a connection can take in all: what it accepts, and what it takes when there's some to spare.</summary>
    private double Room(int edge) => _accept[edge] + _spare[edge];

    /// <summary>What's left of a capacity once <paramref name="used"/> is taken. Unlimited stays unlimited.</summary>
    private static double Left(double capacity, double used) =>
        double.IsPositiveInfinity(capacity) ? double.PositiveInfinity : Math.Max(0, capacity - used);

    /// <summary>
    /// A consumer or station takes everything that comes in, except the fuel it burns: that it takes only when the
    /// line's other consumers have what they want (option 2, step 14). An uploader takes everything that way (BR-A8).
    /// A drone port whose <paramref name="burnRate"/> is known takes its fuel like any consumer, up to that rate; more
    /// than one belt bringing it share that like a merger's inputs (BR-A16).
    /// </summary>
    private void TakeEverything(int node, IReadOnlySet<ItemId>? burns, bool leftoversOnly = false,
        double? burnRate = null)
    {
        List<int> fuelInputs = [];
        foreach (int edge in _in[node])
        {
            List<ItemId> items = [.. _offered[edge].Where(entry => entry.Value > 0).Select(entry => entry.Key)];
            bool fuel = burns is { Count: > 0 } && items.Count > 0 && items.All(burns.Contains);
            if (fuel && burnRate is not null)
            {
                fuelInputs.Add(edge);
                _spare[edge] = 0;
                continue;
            }

            bool spare = leftoversOnly || fuel;
            _accept[edge] = spare ? 0 : double.PositiveInfinity;
            _spare[edge] = spare ? double.PositiveInfinity : 0;
        }

        if (fuelInputs.Count > 0)
        {
            Share(fuelInputs, burnRate!.Value, _accept, new int[fuelInputs.Count]);
        }
    }

    /// <summary>
    /// A node that can pass on <paramref name="available"/>, and <paramref name="spare"/> on top: its inputs share
    /// each like a merger's (BR-A5).
    /// </summary>
    private void ShareInputs(int node, double available, double spare)
    {
        int[] priorities = [.. _in[node].Select(edge => PriorityOf(node, edge))];
        Share(_in[node], available, _accept, priorities);
        Share(_in[node], spare, _spare, priorities);
    }

    /// <summary>BR-A5: the priority of a merger's input; a plain merger's inputs, and everything else's, are all 0.</summary>
    private int PriorityOf(int node, int edge) =>
        _network.Nodes[node] is FlowMerger { Priorities: { Count: > 0 } priorities }
        && _network.Ports[edge].To is int port && port < priorities.Count
            ? priorities[port]
            : 0;

    /// <summary>
    /// Shares <paramref name="available"/> over the inputs by what each offers: the highest priority first, evenly
    /// within one priority, and what's left to the next priority down.
    /// </summary>
    private void Share(List<int> inputs, double available, double[] accept, int[] priorities)
    {
        if (inputs.Count == 1)
        {
            accept[inputs[0]] = available;
            return;
        }

        double[] offers = [.. inputs.Select(edge => Total(_offered[edge]))];
        double[] taken = new double[inputs.Count];
        double left = available;
        foreach (int priority in priorities.Distinct().OrderDescending())
        {
            int[] group = [.. Enumerable.Range(0, inputs.Count).Where(k => priorities[k] == priority)];
            double[] share = Waterfill(left, [.. group.Select(k => offers[k])]);
            for (int j = 0; j < group.Length; j++)
            {
                taken[group[j]] = share[j];
            }

            left = Left(left, share.Sum());
        }

        double total = taken.Sum();
        for (int k = 0; k < inputs.Count; k++)
        {
            // An input may take what the others leave, so an input that offers more later can grow into it.
            accept[inputs[k]] = available - (total - taken[k]);
        }
    }

    /// <summary>
    /// BR-A1: of each input item, a machine takes what its scarcest other input and its outputs allow. When several
    /// belts bring the same item, they share that like a merger's inputs: evenly, by what each offers. A belt
    /// bringing an item it doesn't ask for clogs: nothing comes in over it.
    /// </summary>
    private void AcceptForMachine(int node, FlowMachine machine)
    {
        (double outputs, _) = OutputRoom(node, machine);
        Dictionary<ItemId, double> offered = Offered(node);
        List<int> inputs = _in[node];
        double[] accept = new double[inputs.Count];
        bool[] clogged = new bool[inputs.Count];
        List<ItemId>[] brings = new List<ItemId>[inputs.Count];

        for (int k = 0; k < inputs.Count; k++)
        {
            int edge = inputs[k];
            brings[k] = [.. _offered[edge].Where(entry => entry.Value > 0).Select(entry => entry.Key)];
            if (brings[k].Count == 0)
            {
                brings[k] = [.. machine.Demand.Keys.Where(item => CanCarry(edge, item))];
            }

            clogged[k] = brings[k].Any(item => !machine.Demand.ContainsKey(item));
        }

        foreach ((ItemId item, double demand) in machine.Demand)
        {
            List<int> bringing =
                [.. Enumerable.Range(0, inputs.Count).Where(k => !clogged[k] && brings[k].Contains(item))];
            if (bringing.Count == 0)
            {
                continue;
            }

            double needed = demand * Math.Min(outputs, ScarcestOtherThan(machine, offered, item));
            // A belt that hasn't offered this item yet may bring any of it.
            double[] taken = Waterfill(needed, [
                .. bringing.Select(k =>
                    _offered[inputs[k]].TryGetValue(item, out double offer) && offer > 0
                        ? offer
                        : double.PositiveInfinity)
            ]);
            double total = taken.Sum();
            for (int j = 0; j < bringing.Count; j++)
            {
                // Each belt may take what the others leave, so one that offers more later can grow into it.
                accept[bringing[j]] += needed - (total - taken[j]);
            }
        }

        for (int k = 0; k < inputs.Count; k++)
        {
            _accept[inputs[k]] = clogged[k] ? 0 : accept[k];
        }
    }

    /// <summary>
    /// The share of 100% its outputs can take, and the product that limits it. Solid products share the outputs
    /// that carry solids, fluid products the ones that carry fluids; the tighter of the two decides.
    /// </summary>
    private (double Room, ItemId? Item) OutputRoom(int node, FlowMachine machine)
    {
        (double Room, ItemId? Item) tightest = (1, null);
        foreach (bool fluid in SteadyState.Forms)
        {
            List<ItemId> products =
            [
                .. machine.Production.Where(product => product.Value > 0 && IsFluid(product.Key) == fluid)
                    .Select(product => product.Key)
            ];
            double making = products.Sum(product => machine.Production[product]);
            if (making <= 0)
            {
                continue;
            }

            double room = Math.Min(1, _out[node].Where(edge => CanCarry(edge, fluid)).Sum(Room) / making);
            if (room < tightest.Room)
            {
                tightest = (room, products[0]);
            }
        }

        return tightest;
    }

    /// <summary>BR-A11: every product its outputs can't take all of at 100%, with the share they can take.</summary>
    private Dictionary<ItemId, double> BlockedProducts(int node, FlowMachine machine)
    {
        Dictionary<ItemId, double> blocked = [];
        foreach (bool fluid in SteadyState.Forms)
        {
            List<ItemId> products =
            [
                .. machine.Production.Where(product => product.Value > 0 && IsFluid(product.Key) == fluid)
                    .Select(product => product.Key)
            ];
            double making = products.Sum(product => machine.Production[product]);
            if (making <= 0)
            {
                continue;
            }

            // Products of one form share the outputs that carry it, as in OutputRoom.
            double room = Math.Min(1, _out[node].Where(edge => CanCarry(edge, fluid)).Sum(Room) / making);
            if (room < SteadyState.RunningFrom)
            {
                products.ForEach(product => blocked[product] = room);
            }
        }

        return blocked;
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
            .Select(demand => ShareOf(machine, demand.Key, offered.GetValueOrDefault(demand.Key), demand.Value))
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
                case FlowSplitter { Rules.Count: > 0 } sorting:
                    SendSorted(node, sorting, Supplied(node), Offered(node));
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

    /// <summary>
    /// BR-A9: a source with an unknown rate sends what is asked of it, of the items asked downstream, up to what its
    /// belts can carry.
    /// </summary>
    private void SendAsked(int node)
    {
        Dictionary<ItemId, double> asked = _asked[node];
        double weight = asked.Values.Sum();
        if (weight <= 0)
        {
            return;
        }

        double total = _out[node].Sum(Room);
        if (double.IsPositiveInfinity(total))
        {
            // Nothing downstream limits it yet; the next backward pass will.
            total = 0;
        }

        // It offers what its belts can carry, whatever is taken so far. A machine with two inputs from two
        // stations would otherwise wait for each: each takes one input only as far as the other is offered.
        double offer = Math.Max(total, _out[node].Sum(CanCarryAtMost));
        // Of each item as much as the machines it reaches ask for, relative to the others.
        Dictionary<ItemId, double> sending =
            asked.ToDictionary(entry => entry.Key, entry => total * entry.Value / weight);
        Dictionary<ItemId, double> offering =
            asked.ToDictionary(entry => entry.Key, entry => offer * entry.Value / weight);
        Send(node, sending, offering);
    }

    /// <summary>What a connection can carry at most: the capacity of the belt or pipe it leads to; else what it takes, if limited.</summary>
    private double CanCarryAtMost(int edge) => _network.Nodes[_network.Edges[edge].To] switch
    {
        FlowConduit { Capacity: < double.PositiveInfinity } conduit => conduit.Capacity,
        _ when !double.IsPositiveInfinity(Room(edge)) => Room(edge),
        _ => 0,
    };

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
        (MachineLimit limit, ItemId? item) = utilisation >= SteadyState.RunningFrom ? (MachineLimit.None, (ItemId?)null)
            : outputs < offeredShare - SteadyState.Settle ? (MachineLimit.OutputBlocked, blocked ?? machine.Production.Keys.First())
            : (MachineLimit.Starved, scarcest);
        Dictionary<ItemId, double> offered = Offered(node);
        Dictionary<ItemId, double> shortOf = machine.Demand
            .Where(demand => demand.Value > 0)
            .Select(demand => (Item: demand.Key,
                Share: ShareOf(machine, demand.Key, offered.GetValueOrDefault(demand.Key), demand.Value)))
            .Where(input => input.Share < SteadyState.RunningFrom)
            .ToDictionary(input => input.Item, input => input.Share);
        // Offered in full but not arriving (its sender is held back itself): what arrives of the item it's starved of.
        if (limit == MachineLimit.Starved && shortOf.Count == 0 && item is { } starvedOf &&
            machine.Demand.TryGetValue(starvedOf, out double needed))
        {
            shortOf[starvedOf] = Math.Min(1, Supplied(node).GetValueOrDefault(starvedOf) / needed);
        }

        _machines[machine.Id] = new MachineState(machine.Id, Math.Min(1, utilisation), limit, item, shortOf,
            BlockedProducts(node, machine));

        Send(node,
            machine.Production.ToDictionary(product => product.Key, product => product.Value * utilisation),
            machine.Production.ToDictionary(product => product.Key, product => product.Value * offeredShare));
    }

    /// <summary>The share of its demand a machine has of an input: all of it when its stock lasts (BR-A17).</summary>
    private static double ShareOf(FlowMachine machine, ItemId item, double has, double demand) =>
        machine.Stocked?.Contains(item) == true ? 1 : Math.Min(1, has / demand);

    /// <summary>The lowest share of its demand a machine gets of any input, and which item that is.</summary>
    private static (double Share, ItemId? Item) Scarcest(FlowMachine machine, Dictionary<ItemId, double> per) =>
        machine.Demand
            .Where(demand => demand.Value > 0)
            .Select(demand => (Share: ShareOf(machine, demand.Key, per.GetValueOrDefault(demand.Key), demand.Value),
                Item: (ItemId?)demand.Key))
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
        double[] spare = [.. outputs.Select(edge => _spare[edge])];
        foreach (bool fluid in SteadyState.Forms)
        {
            Dictionary<ItemId, double> sending = OfForm(items, fluid);
            Dictionary<ItemId, double> offering = OfForm(offer, fluid);
            if (sending.Count == 0 && offering.Count == 0)
            {
                continue;
            }

            bool[] carries = [.. outputs.Select(edge => CanCarry(edge, fluid))];
            double total = Total(sending);
            // First what the outputs accept, then what's left over to the ones that take spare.
            double[] firm = Waterfill(total,
                [.. Enumerable.Range(0, outputs.Count).Select(k => carries[k] ? room[k] : 0)]);
            double[] extra = Waterfill(total - firm.Sum(),
                [.. Enumerable.Range(0, outputs.Count).Select(k => carries[k] ? spare[k] : 0)]);
            double[] sent = [.. Enumerable.Range(0, outputs.Count).Select(k => firm[k] + extra[k])];
            // Spare is taken only from what's left over, so it doesn't lower what the other outputs are offered.
            double allFirm = firm.Sum();
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

                double offeredHere = Math.Max(0, offerTotal - (allFirm - firm[k]));
                if (offerTotal > 0)
                {
                    foreach ((ItemId item, double rate) in offering)
                    {
                        _offered[outputs[k]][item] = rate * offeredHere / offerTotal;
                    }
                }

                room[k] -= firm[k];
                spare[k] -= extra[k];
            }
        }
    }

    /// <summary>The rules of the splitter output a connection leaves from. None when its port isn't known.</summary>
    private IEnumerable<SortRule> RulesAt(FlowSplitter splitter, int edge) =>
        _network.Ports[edge].From is { } port ? splitter.Rules!.Where(rule => rule.Output == port) : [];

    /// <summary>
    /// BR-A4: a smart or programmable splitter sends each item to the outputs its rules name: the item itself,
    /// <i>any</i>, or <i>any undefined</i> when no output names it. Evenly over those, as far as they take it; what
    /// they can't take goes to the <i>overflow</i> outputs. An output set to <i>none</i> gets nothing.
    /// Items going to the same outputs are sent together; groups are served one after the other. Spare, room taken only
    /// from what's left (an uploader, a drone port's fuel), is served last: after the overflow outputs (BR-A8).
    /// </summary>
    private void SendSorted(int node, FlowSplitter splitter, Dictionary<ItemId, double> items,
        Dictionary<ItemId, double> offer)
    {
        List<int> outputs = _out[node];
        foreach (int edge in outputs)
        {
            _flow[edge].Clear();
            _offered[edge].Clear();
        }

        List<SortRule>[] rules = [.. outputs.Select(edge => RulesAt(splitter, edge).ToList())];
        HashSet<ItemId> named =
            [.. splitter.Rules!.Where(rule => rule.Kind == SortRuleKind.Item).Select(rule => rule.Item!)];

        bool Takes(int k, ItemId item) => CanCarry(outputs[k], item) && rules[k].Any(rule => rule.Kind switch
        {
            SortRuleKind.Any => true,
            SortRuleKind.Item => rule.Item == item,
            SortRuleKind.AnyUndefined => !named.Contains(item),
            _ => false,
        });

        bool Overflows(int k, ItemId item) =>
            CanCarry(outputs[k], item) && rules[k].Any(rule => rule.Kind == SortRuleKind.Overflow);

        double[] room = [.. outputs.Select(edge => _accept[edge])];
        double[] spare = [.. outputs.Select(edge => _spare[edge])];
        var groups = items.Keys.Union(offer.Keys)
            .GroupBy(item => string.Join(",",
                Enumerable.Range(0, outputs.Count).Select(k => Takes(k, item) ? "t" : Overflows(k, item) ? "o" : "-")))
            .OrderBy(group => group.Key, StringComparer.Ordinal);

        foreach (IGrouping<string, ItemId> group in groups)
        {
            ItemId first = group.First();
            bool[] takes = [.. Enumerable.Range(0, outputs.Count).Select(k => Takes(k, first))];
            bool[] overflows = [.. Enumerable.Range(0, outputs.Count).Select(k => !takes[k] && Overflows(k, first))];
            Dictionary<ItemId, double>
                sending = group.ToDictionary(item => item, item => items.GetValueOrDefault(item));
            Dictionary<ItemId, double> offering =
                group.ToDictionary(item => item, item => offer.GetValueOrDefault(item));
            double total = Total(sending);
            double offerTotal = Total(offering);

            // What the outputs take firmly first, the main ones before the overflow; then the spare, in the same order.
            double[] main = Waterfill(total,
                [.. Enumerable.Range(0, outputs.Count).Select(k => takes[k] ? room[k] : 0)]);
            double[] over = Waterfill(total - main.Sum(),
                [.. Enumerable.Range(0, outputs.Count).Select(k => overflows[k] ? room[k] : 0)]);
            double[] mainSpare = Waterfill(total - main.Sum() - over.Sum(),
                [.. Enumerable.Range(0, outputs.Count).Select(k => takes[k] ? spare[k] : 0)]);
            double[] overSpare = Waterfill(
                total - main.Sum() - over.Sum() - mainSpare.Sum(),
                [.. Enumerable.Range(0, outputs.Count).Select(k => overflows[k] ? spare[k] : 0)]);
            // Spare is taken only from what's left over, so it doesn't lower what the other outputs are offered.
            double mainFirm = main.Sum();
            double overFirm = over.Sum();
            double[] mainFirmOf = [.. main];
            double[] overFirmOf = [.. over];
            for (int k = 0; k < outputs.Count; k++)
            {
                main[k] += mainSpare[k];
                over[k] += overSpare[k];
            }

            for (int k = 0; k < outputs.Count; k++)
            {
                if (!takes[k] && !overflows[k])
                {
                    continue;
                }

                double sent = main[k] + over[k];
                // The main outputs are offered all but what the other main outputs take; an overflow output only
                // what the main outputs leave.
                double offeredHere = takes[k]
                    ? Math.Max(0, offerTotal - (mainFirm - mainFirmOf[k]))
                    : Math.Max(0, offerTotal - mainFirm - (overFirm - overFirmOf[k]));
                foreach (ItemId item in group)
                {
                    if (total > 0)
                    {
                        _flow[outputs[k]][item] = sending[item] * sent / total;
                    }

                    if (offerTotal > 0)
                    {
                        _offered[outputs[k]][item] = offering[item] * offeredHere / offerTotal;
                    }
                }

                double firmSent = Math.Min(sent, room[k]);
                room[k] -= firmSent;
                spare[k] -= sent - firmSent;
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
                _network.Nodes[_network.Edges[edge].From] is FlowConduit upstream &&
                Saturated(_network.Edges[edge].From, upstream));
            if (flowing >= conduit.Capacity - SteadyState.Tolerance && offered - flowing > SteadyState.Tolerance && !fedBySaturated)
            {
                bottlenecks.Add(new Bottleneck(conduit.Id, conduit.Capacity, flowing, offered - flowing));
            }
        }

        return bottlenecks;
    }

    private bool Saturated(int node, FlowConduit conduit) =>
        _in[node].Sum(edge => Total(_flow[edge])) >= conduit.Capacity - SteadyState.Tolerance;

    private List<UnusedSupply> Unused()
    {
        List<UnusedSupply> unused = [];
        for (int node = 0; node < _network.Nodes.Count; node++)
        {
            if (_network.Nodes[node] is FlowProvider provider)
            {
                double delivered = _out[node].Sum(edge => Total(_flow[edge]));
                if (provider.Offer - delivered > SteadyState.Tolerance)
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
                foreach ((ItemId item, double rate) in _flow[edge].Where(entry => entry.Value > SteadyState.Tolerance))
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
            unknown.Add(new UnknownSupply(source.Id, delivered, minutes, minutes >= SteadyState.ConfirmedMinutes - SteadyState.Settle));
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

        while (open.Count > 0 && left > SteadyState.Settle)
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

    /// <summary>
    /// For a source with an unknown rate: the items the machines it feeds ask for, with how much they ask in all,
    /// narrowed to the items it holds.
    /// </summary>
    private Dictionary<ItemId, double> AskedItems(int node)
    {
        if (_network.Nodes[node] is not FlowUnknownSource source)
        {
            return [];
        }

        Dictionary<ItemId, double> asked = [];

        void Ask(IEnumerable<ItemId> items, double rate, int edge)
        {
            foreach (ItemId item in items.Where(item => CanCarry(edge, item)))
            {
                asked[item] = asked.GetValueOrDefault(item) + rate;
            }
        }

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
                        foreach ((ItemId item, double rate) in
                                 machine.Demand.Where(demand => CanCarry(edge, demand.Key)))
                        {
                            asked[item] = asked.GetValueOrDefault(item) + rate;
                        }

                        break;
                    // BR-A16: a drone port burning a known rate asks for its fuel, like a generator.
                    case FlowSink { BurnRate: { } rate, Burns: { Count: > 0 } fuels }:
                        Ask(fuels, rate, edge);
                        break;
                    case FlowUnknownSource { BurnRate: { } rate, Burns: { Count: > 0 } fuels }:
                        Ask(fuels, rate, edge);
                        break;
                    case FlowConduit or FlowSplitter or FlowMerger:
                        next.Enqueue(to);
                        break;
                }
            }
        }

        // What it holds narrows it down when it's known; a station holding nothing right now still supplies what's asked.
        HashSet<ItemId> held = [.. source.Stock.Where(stock => stock.Value > 0).Select(stock => stock.Key)];
        return held.Count > 0 && held.Overlaps(asked.Keys)
            ? asked.Where(entry => held.Contains(entry.Key)).ToDictionary(entry => entry.Key, entry => entry.Value)
            : asked;
    }
}