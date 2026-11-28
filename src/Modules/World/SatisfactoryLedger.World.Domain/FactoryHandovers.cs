using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.Domain.Flow.Objects;
using SatisfactoryLedger.World.Domain.Tracing;

namespace SatisfactoryLedger.World.Domain;

/// <summary>
/// A factory's handovers (card 3, decided 2026-11-27): per item, what crosses its inputs and its outputs. What it can't derive from the
/// world and the analysis yet comes from its caller as facts; every rule on them is here.
/// </summary>
public sealed class FactoryHandovers
{
    /// <summary>Machines ordered by id, as the factory page and the arrangement list them.</summary>
    private static readonly IComparer<BuildableId> ById = Comparer<BuildableId>.Create((a, b) => string.CompareOrdinal(a.Id, b.Id));

    /// <summary>The outputs side, worked out the first time <see cref="Outputs"/> is read: the arrangement reads only the inputs.</summary>
    private readonly Lazy<IReadOnlyList<OutgoingItem>> _outputs;

    private FactoryHandovers(IReadOnlyList<IncomingItem> inputs, Func<IReadOnlyList<OutgoingItem>> outputs)
    {
        Inputs = inputs;
        _outputs = new Lazy<IReadOnlyList<OutgoingItem>>(outputs);
    }

    /// <summary>What it takes in, per item, ordered by item id.</summary>
    public IReadOnlyList<IncomingItem> Inputs { get; }

    /// <summary>What it makes, per item that leaves, ordered by item id. Worked out once, when first read.</summary>
    public IReadOnlyList<OutgoingItem> Outputs => _outputs.Value;

    /// <param name="members">The factory's members in <paramref name="world"/>.</param>
    /// <param name="owners">Who each machine belongs to: this factory's own members, and the other factories' machines.</param>
    /// <param name="sinks">The AWESOME Sinks: what reaches them is sunk.</param>
    /// <param name="usedUp">The consumers that use up what they get, such as a portal: shown by building (decided 2026-11-11).</param>
    /// <param name="buildingOf">The class of each building, such as <c>Build_MinerMk3_C</c>.</param>
    public static FactoryHandovers Of(
        WorldGraph world,
        FlowReadings readings,
        FactoryMembers members,
        IReadOnlyDictionary<BuildableId, Guid> owners,
        IReadOnlySet<BuildableId> sinks,
        IReadOnlySet<BuildableId> usedUp,
        IReadOnlyDictionary<BuildableId, string> buildingOf) =>
        new(IncomingOf(world, readings, members, owners, sinks, buildingOf), () => OutgoingOf(world, readings, members, owners, sinks, usedUp, buildingOf));

    /// <summary>
    /// What leaves over an output of one item now, read from the stored flows: they keep what flows into each belt, lift,
    /// pipe and container in a line (ADR-0017).
    /// <list type="bullet">
    /// <item>into a storage container at the end of the line: what flows into the logistics that feed it;</item>
    /// <item>from logistics with one way out: what flows into it;</item>
    /// <item>otherwise into the logistics across. When that's fed from elsewhere too, such as a pipe network shared
    /// with other factories, this counts their part as well.</item>
    /// </list>
    /// The factory preview reads it per output, before there's a factory that owns machines.
    /// </summary>
    public static double Leaving(WorldGraph world, FlowReadings readings, FactoryConnection output, ItemId item)
    {
        if (output.Outside is not { } outside)
        {
            return world.Upstream(output.Inside).Sum(feeder => readings.Carries(feeder.Id, item));
        }

        if (world.Find(output.Inside) is Logistics && world.Downstream(output.Inside).Count() == 1)
        {
            return readings.Carries(output.Inside, item);
        }

        return world.Find(outside) is Logistics ? readings.Carries(outside, item) : 0;
    }

    /// <summary>What it takes in, per item: see <see cref="IncomingItem"/>.</summary>
    private static List<IncomingItem> IncomingOf(
        WorldGraph world,
        FlowReadings readings,
        FactoryMembers members,
        IReadOnlyDictionary<BuildableId, Guid> owners,
        IReadOnlySet<BuildableId> sinks,
        IReadOnlyDictionary<BuildableId, string> buildingOf)
    {
        Dictionary<ItemId, double> now = [];
        Dictionary<ItemId, List<BuildableId>> startsOf = [];
        Dictionary<ItemId, Dictionary<Source, double>> sources = [];

        // Per item and source, the machines and generators it starts at.
        Dictionary<(ItemId Item, Source Source), SortedSet<BuildableId>> startMachines = [];
        foreach (FactoryConnection input in members.Inputs)
        {
            BuildableId start = input.Outside ?? input.Inside;
            foreach (ItemId item in input.Items)
            {
                double amount = Arriving(world, readings, members, input, item);
                now[item] = now.GetValueOrDefault(item) + amount;

                // From its own belt or pipe when it has one, so that one's room counts too.
                startsOf.TryAdd(item, []);
                startsOf[item].Add(world.Find(input.Inside) is Logistics ? input.Inside : start);

                Dictionary<Source, double> from = sources.TryGetValue(item, out var found) ? found : sources[item] = [];
                foreach ((BuildableId source, double perMinute) in InputSources.Of(world, start, item, amount, readings))
                {
                    Source key = SourceOf(source);
                    from[key] = from.GetValueOrDefault(key) + perMinute;
                    if (key.Kind is InputSourceKind.Factory or InputSourceKind.NoFactory)
                    {
                        (startMachines.TryGetValue((item, key), out SortedSet<BuildableId>? machines) ? machines : startMachines[(item, key)] = new(ById)).Add(source);
                    }
                }
            }
        }

        // At 100%, what its machines take of it less what its own machines make of it: what it needs from outside.
        Dictionary<ItemId, double> atFullSpeed = AtFullSpeed(world, members, now, member => member.Demand, member => member.Production);

        return
        [
            .. now.Keys.OrderBy(item => item.Id, StringComparer.Ordinal)
                .Select(item => new IncomingItem(
                    item,
                    now[item],
                    Math.Max(0, atFullSpeed.GetValueOrDefault(item)),
                    [
                        .. (sources.GetValueOrDefault(item) ?? []).OrderByDescending(entry => entry.Value).Select(entry => new IncomingSource(
                            entry.Key.Kind,
                            entry.Key.Owner,
                            entry.Key.Building,
                            entry.Key.Id,
                            entry.Value,
                            startMachines.TryGetValue((item, entry.Key), out SortedSet<BuildableId>? machines) ? [.. machines] : [])),
                    ],
                    () => CouldBringMore(world, readings, startsOf[item], item, id => Spare(id, item), id => Sunk(id, item)))),
        ];

        // Machines by factory; a building on its own, so two miners on one belt are two sources (decided 2026-11-11).
        Source SourceOf(BuildableId start) => world.Find(start) switch
        {
            Machine or Consumer { IsGenerator: true } when owners.TryGetValue(start, out Guid owner) => new(InputSourceKind.Factory, owner, null, null),
            Machine or Consumer { IsGenerator: true } => new(InputSourceKind.NoFactory, null, null, null),
            Logistics { Kind: LogisticsKind.Belt or LogisticsKind.Lift or LogisticsKind.Pipe or LogisticsKind.PipeNetwork } => new(InputSourceKind.Nowhere, null, null, null),
            _ => new(InputSourceKind.Building, null, buildingOf.GetValueOrDefault(start), start),
        };

        // What an AWESOME Sink takes of an item now: what reaches it over its lines.
        double Sunk(BuildableId id, ItemId item) => sinks.Contains(id) ? readings.Reaching(id, item) : 0;

        // What a source could send more per minute: a provider its unused supply; a station what its stock keeps up
        // for half an hour; a machine of another factory, or of none, what it would make more at 100%. Its own machines
        // aren't outside supply, and a container's rate isn't known: none.
        double Spare(BuildableId id, ItemId item)
        {
            if (members.Machines.Contains(id))
            {
                return 0;
            }

            return world.Find(id) switch
            {
                Provider { IsStation: true } station => StationSpare(station, item),
                Provider => readings.Spare(id, item),
                null => 0,
                _ => readings.MakesMore(id, item),
            };
        }

        // BR-A17 turned round (decided 2026-11-11): a station could deliver more as far as its stock, with that of the
        // containers its line unloads into, lasts half an hour at the higher rate. The rate trains or trucks bring
        // isn't in the save.
        double StationSpare(Provider station, ItemId item)
        {
            double delivered = readings.Delivered(station.Id, item);
            double stock = (double)(station.Stock.GetValueOrDefault(item) + Buffers(station.Id).Sum(buffer => buffer.Stock.GetValueOrDefault(item)));
            return Math.Max(0, (stock / SteadyState.ConfirmedMinutes) - delivered);
        }

        // The containers a station's line passes through, as far as the machines it reaches.
        IEnumerable<Logistics> Buffers(BuildableId station)
        {
            HashSet<BuildableId> seen = [station];
            Queue<BuildableId> toVisit = new([station]);
            while (toVisit.TryDequeue(out BuildableId? at))
            {
                foreach (Logistics next in world.Downstream(at).OfType<Logistics>().Where(next => seen.Add(next.Id)))
                {
                    if (next.Kind is LogisticsKind.StorageContainer or LogisticsKind.FluidBuffer)
                    {
                        yield return next;
                    }

                    toVisit.Enqueue(next.Id);
                }
            }
        }
    }

    /// <summary>What it makes, per item that leaves: see <see cref="OutgoingItem"/>.</summary>
    private static List<OutgoingItem> OutgoingOf(
        WorldGraph world,
        FlowReadings readings,
        FactoryMembers members,
        IReadOnlyDictionary<BuildableId, Guid> owners,
        IReadOnlySet<BuildableId> sinks,
        IReadOnlySet<BuildableId> usedUp,
        IReadOnlyDictionary<BuildableId, string> buildingOf)
    {
        Dictionary<ItemId, double> now = [];
        Dictionary<ItemId, List<BuildableId>> startsOf = [];
        foreach (FactoryConnection output in members.Outputs)
        {
            foreach (ItemId item in output.Items)
            {
                now[item] = now.GetValueOrDefault(item) + Leaving(world, readings, output, item);
                startsOf.TryAdd(item, []);
                startsOf[item].Add(output.Outside ?? output.Inside);
            }
        }

        Dictionary<ItemId, Dictionary<Destination, double>> destinations = [];
        Dictionary<ItemId, double> brings = [];
        foreach ((BuildableId end, ItemId item, double perMinute) in Ends(world, readings, members))
        {
            Dictionary<Destination, double> to = destinations.TryGetValue(item, out var found) ? found : destinations[item] = [];
            Destination key = DestinationOf(end);
            to[key] = to.GetValueOrDefault(key) + perMinute;

            // What this factory brings its receivers: what reaches one that asks for the item, a drone port too.
            if (world.Find(end) is { } receiver && receiver.DemandOf(item) > Rate.Zero)
            {
                brings[item] = brings.GetValueOrDefault(item) + perMinute;
            }
        }

        // At 100%, what its machines make of it less what its own machines take of it (decided 2026-11-11): a loop
        // that makes rubber and takes some back for its plastic only lets the rest leave. Only what leaves: a product
        // its own machines take all of, such as gas unpacked for its blenders, isn't output.
        Dictionary<ItemId, double> atFullSpeed = AtFullSpeed(world, members, now, member => member.Production, member => member.Demand);

        // What an output blocked producer would make more at 100% but can't get out.
        Dictionary<ItemId, double> cannotLeave = [];
        foreach (Buildable producer in members.Producers.Select(id => world.Find(id)).OfType<Buildable>())
        {
            if (readings.Machine(producer.Id) is not { Limit: MachineLimit.OutputBlocked })
            {
                continue;
            }

            foreach (ItemId item in producer.Production.Keys.Where(now.ContainsKey))
            {
                cannotLeave[item] = cannotLeave.GetValueOrDefault(item) + readings.MakesMore(producer.Id, item);
            }
        }

        return
        [
            .. now.Keys.OrderBy(item => item.Id, StringComparer.Ordinal).Select(item =>
            {
                Dictionary<Destination, double> to = destinations.GetValueOrDefault(item) ?? [];
                List<Buildable> receivers = [.. Receivers(world, startsOf[item])];
                double need = receivers.Aggregate(Rate.Zero, (sum, receiver) => sum + receiver.DemandOf(item)).ToDouble();
                double get = receivers.Where(receiver => receiver.DemandOf(item) > Rate.Zero).Sum(receiver => readings.Into(receiver.Id, item));
                return new OutgoingItem(
                    item,
                    now[item],
                    Math.Max(0, atFullSpeed.GetValueOrDefault(item)),
                    need,
                    Math.Max(0, get - brings.GetValueOrDefault(item)),
                    Math.Max(0, need - get),
                    to.Where(entry => entry.Key.Kind == OutputDestinationKind.Sink).Sum(entry => entry.Value),
                    cannotLeave.GetValueOrDefault(item),
                    [
                        .. to.OrderByDescending(entry => entry.Value)
                            .Select(entry => new OutgoingDestination(entry.Key.Kind, entry.Key.Owner, entry.Key.Building, entry.Value)),
                    ]);
            }),
        ];

        // The machines and generators of a factory by that factory; a consumer that uses up what it gets by its building.
        Destination DestinationOf(BuildableId end) => world.Find(end) switch
        {
            Machine or Consumer { IsGenerator: true } when owners.TryGetValue(end, out Guid owner) => new(OutputDestinationKind.Factory, owner, null),
            Machine or Consumer { IsGenerator: true } => new(OutputDestinationKind.NoFactory, null, null),
            Consumer when sinks.Contains(end) => new(OutputDestinationKind.Sink, null, null),
            Consumer when usedUp.Contains(end) => new(OutputDestinationKind.Consumer, null, buildingOf.GetValueOrDefault(end)),
            Logistics { Kind: LogisticsKind.Belt or LogisticsKind.Lift or LogisticsKind.Pipe or LogisticsKind.PipeNetwork } => new(OutputDestinationKind.Nowhere, null, null),
            _ => new(OutputDestinationKind.Storage, null, null),
        };
    }

    /// <summary>
    /// At 100%, per item that crosses the factory's handovers, what its machines' <paramref name="adding"/> rates come to
    /// less their <paramref name="takingOff"/> rates: demand less production for what it needs from outside, production
    /// less demand for what it lets leave. Each machine adds, then takes off, in that order.
    /// </summary>
    private static Dictionary<ItemId, double> AtFullSpeed(
        WorldGraph world,
        FactoryMembers members,
        Dictionary<ItemId, double> crossing,
        Func<Buildable, IReadOnlyDictionary<ItemId, Rate>> adding,
        Func<Buildable, IReadOnlyDictionary<ItemId, Rate>> takingOff)
    {
        Dictionary<ItemId, double> net = [];
        foreach (Buildable member in members.Machines.Select(id => world.Find(id)).OfType<Buildable>())
        {
            foreach ((ItemId item, Rate rate) in adding(member).Where(entry => crossing.ContainsKey(entry.Key)))
            {
                net[item] = net.GetValueOrDefault(item) + rate.ToDouble();
            }

            foreach ((ItemId item, Rate rate) in takingOff(member).Where(entry => crossing.ContainsKey(entry.Key)))
            {
                net[item] = net.GetValueOrDefault(item) - rate.ToDouble();
            }
        }

        return net;
    }

    /// <summary>
    /// The machines and consumers the lines from <paramref name="from"/> reach through logistics, each once, however
    /// many of the lines reach it. Whatever else feeds them.
    /// </summary>
    private static IEnumerable<Buildable> Receivers(WorldGraph world, IEnumerable<BuildableId> from)
    {
        HashSet<BuildableId> seen = [.. from];
        Queue<BuildableId> next = new(seen);
        while (next.TryDequeue(out BuildableId? at))
        {
            Buildable? buildable = world.Find(at);
            if (buildable is not Logistics)
            {
                if (buildable is not null)
                {
                    yield return buildable;
                }

                continue;
            }

            foreach (Buildable onwards in world.Downstream(at).Where(onwards => seen.Add(onwards.Id)))
            {
                next.Enqueue(onwards.Id);
            }
        }
    }

    /// <summary>
    /// What leaves a factory over its outputs, per building at the end of its lines and item, per minute (BR-A18): a
    /// machine of another factory, a sink, a station. In the order its outputs come, as the arrangement reads them.
    /// </summary>
    public static IEnumerable<(BuildableId End, ItemId Item, double PerMinute)> Ends(WorldGraph world, FlowReadings readings, FactoryMembers members)
    {
        foreach (FactoryConnection output in members.Outputs)
        {
            BuildableId start = output.Outside ?? output.Inside;
            foreach (ItemId item in output.Items)
            {
                foreach ((BuildableId end, double perMinute) in OutputDestinations.Of(world, start, item, Leaving(world, readings, output, item), readings))
                {
                    yield return (end, item, perMinute);
                }
            }
        }
    }

    /// <summary>
    /// What arrives over an input of one item now, read from the stored flows as for an output:
    /// <list type="bullet">
    /// <item>from a container nothing feeds: what flows into the logistics it feeds;</item>
    /// <item>into logistics with one way in: what flows into it;</item>
    /// <item>from logistics with one way out: what flows into that;</item>
    /// <item>otherwise (a machine fed from a splitter or pipe network shared with others): what the machine takes at the
    /// rate it runs, shared over its inputs that bring the item.</item>
    /// </list>
    /// The factory preview reads it per input, before there's a factory that owns machines.
    /// </summary>
    public static double Arriving(WorldGraph world, FlowReadings readings, FactoryMembers members, FactoryConnection input, ItemId item)
    {
        if (input.Outside is not { } outside)
        {
            return world.Downstream(input.Inside).Sum(next => readings.Carries(next.Id, item));
        }

        if (world.Find(input.Inside) is Logistics && world.Upstream(input.Inside).Count() == 1)
        {
            return readings.Carries(input.Inside, item);
        }

        if (world.Find(outside) is Logistics && world.Downstream(outside).Count() == 1)
        {
            return readings.Carries(outside, item);
        }

        return readings.Takes(input.Inside, item) / members.Inputs.Count(other => other.Inside == input.Inside && other.Items.Contains(item));
    }

    /// <summary>
    /// How much more of <paramref name="item"/> could reach <paramref name="to"/> than does now (decided 2026-11-11):
    /// what its sources have to spare, and what branches off its line into a sink, as far as the room left on the
    /// logistics between lets it through (<see cref="FlowReadings.Room"/>). Sources sharing a line share its room. This
    /// is a maximum flow from the sources to the inputs, each piece of logistics passing at most its room; what flows
    /// into a sink now may be sent back up its branch, as far as the branch carries it, to come to the inputs instead.
    /// </summary>
    /// <param name="spare">What a source could send more per minute.</param>
    /// <param name="sunk">What a sink takes of the item now, per minute; nothing for anything else.</param>
    private static double CouldBringMore(
        WorldGraph world,
        FlowReadings readings,
        IEnumerable<BuildableId> to,
        ItemId item,
        Func<BuildableId, double> spare,
        Func<BuildableId, double> sunk)
    {
        // The line upstream of the inputs, as far as its sources.
        List<BuildableId> starts = [.. to.Distinct()];
        List<BuildableId> nodes = [];
        Dictionary<BuildableId, int> index = [];
        HashSet<BuildableId> line = [];
        Queue<BuildableId> toVisit = new(starts);
        while (toVisit.TryDequeue(out BuildableId? at))
        {
            if (Add(at))
            {
                line.Add(at);
                if (IsPassedThrough(at))
                {
                    foreach (Buildable from in world.Upstream(at))
                    {
                        toVisit.Enqueue(from.Id);
                    }
                }
            }
        }

        // The branches off that line, as far as their ends: what goes into a sink there could come instead.
        Queue<BuildableId> branches = new(line.Where(IsPassedThrough).SelectMany(at => world.Downstream(at)).Select(next => next.Id).Where(next => !line.Contains(next)));
        while (branches.TryDequeue(out BuildableId? at))
        {
            if (Add(at) && world.Find(at) is Logistics)
            {
                foreach (Buildable next in world.Downstream(at))
                {
                    branches.Enqueue(next.Id);
                }
            }
        }

        // Each buildable is two points, in and out; logistics passes at most its room from one to the other.
        var flow = new MaximumFlow((nodes.Count * 2) + 2);
        int source = nodes.Count * 2;
        int sink = source + 1;
        foreach (BuildableId node in nodes)
        {
            if (line.Contains(node) && !IsPassedThrough(node))
            {
                flow.Add(source, Out(node), Math.Max(0, spare(node)));
                continue;
            }

            if (world.Find(node) is Logistics)
            {
                // On a branch, what it carries now may go back the way it came.
                flow.Add(In(node), Out(node), Math.Max(0, readings.Room(node)), line.Contains(node) ? 0 : Math.Max(0, readings.Carries(node, item)));
            }
            else
            {
                flow.Add(source, In(node), Math.Max(0, sunk(node)));
            }

            foreach (Buildable from in world.Upstream(node).Where(from => index.ContainsKey(from.Id)))
            {
                flow.Add(Out(from.Id), In(node), double.PositiveInfinity, line.Contains(node) ? 0 : double.PositiveInfinity);
            }
        }

        foreach (BuildableId start in starts)
        {
            flow.Add(Out(start), sink, double.PositiveInfinity);
        }

        return flow.From(source, sink);

        bool Add(BuildableId id)
        {
            if (index.ContainsKey(id))
            {
                return false;
            }

            index[id] = nodes.Count;
            nodes.Add(id);
            return true;
        }

        int In(BuildableId id) => 2 * index[id];

        int Out(BuildableId id) => (2 * index[id]) + 1;

        // Logistics fed from upstream passes things on; anything else, or logistics nothing feeds, is a source.
        bool IsPassedThrough(BuildableId id) => world.Find(id) is Logistics && world.Upstream(id).Any();
    }

    /// <summary>A source as it's grouped: what arrives from the same one adds up.</summary>
    private readonly record struct Source(InputSourceKind Kind, Guid? Owner, string? Building, BuildableId? Id);

    /// <summary>A destination as it's grouped: what goes to the same one adds up.</summary>
    private readonly record struct Destination(OutputDestinationKind Kind, Guid? Owner, string? Building);

    /// <summary>A maximum flow by shortest augmenting paths (Edmonds-Karp): the graphs here are a line or two upstream.</summary>
    private sealed class MaximumFlow(int points)
    {
        private readonly List<double> _capacity = [];
        private readonly List<int>[] _out = [.. Enumerable.Range(0, points).Select(_ => new List<int>())];
        private readonly List<int> _to = [];

        /// <summary>An edge, and its way back: what it may carry back before anything has gone forward.</summary>
        public void Add(int from, int to, double capacity, double back = 0)
        {
            _out[from].Add(_to.Count);
            _to.Add(to);
            _capacity.Add(capacity);
            _out[to].Add(_to.Count);
            _to.Add(from);
            _capacity.Add(back);
        }

        public double From(int source, int sink)
        {
            double total = 0;
            while (true)
            {
                int[] via = Enumerable.Repeat(-1, _out.Length).ToArray();
                Queue<int> queue = new([source]);
                via[source] = -2;
                while (queue.TryDequeue(out int at) && via[sink] == -1)
                {
                    foreach (int edge in _out[at].Where(edge => _capacity[edge] > 1e-9 && via[_to[edge]] == -1))
                    {
                        via[_to[edge]] = edge;
                        queue.Enqueue(_to[edge]);
                    }
                }

                if (via[sink] == -1)
                {
                    return total;
                }

                double bottleneck = double.PositiveInfinity;
                for (int at = sink; at != source; at = _to[via[at] ^ 1])
                {
                    bottleneck = Math.Min(bottleneck, _capacity[via[at]]);
                }

                if (double.IsPositiveInfinity(bottleneck))
                {
                    return double.PositiveInfinity;
                }

                for (int at = sink; at != source; at = _to[via[at] ^ 1])
                {
                    _capacity[via[at]] -= bottleneck;
                    _capacity[via[at] ^ 1] += bottleneck;
                }

                total += bottleneck;
            }
        }
    }
}
