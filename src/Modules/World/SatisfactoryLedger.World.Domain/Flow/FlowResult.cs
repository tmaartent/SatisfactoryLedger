namespace SatisfactoryLedger.World.Domain.Flow;

/// <summary>The steady state of a flow network: approximations to 0.0001/min (BR-A10, ADR-0004).</summary>
public sealed class FlowResult
{
    private readonly FlowNetwork _network;
    private readonly IReadOnlyList<IReadOnlyDictionary<ItemId, double>> _flows;
    private readonly IReadOnlyDictionary<BuildableId, MachineState> _machines;
    private readonly Dictionary<BuildableId, FlowPart> _partOf;
    private ILookup<int, int>? _incoming;

    public FlowResult(
        FlowNetwork network,
        IReadOnlyList<IReadOnlyDictionary<ItemId, double>> flows,
        IReadOnlyDictionary<BuildableId, MachineState> machines,
        IReadOnlyList<Bottleneck> bottlenecks,
        IReadOnlyList<UnusedSupply> unusedSupply,
        IReadOnlyList<UnknownSupply> unknownSupply,
        IReadOnlyList<FlowPart> parts)
    {
        _network = network;
        _flows = flows;
        _machines = machines;
        Bottlenecks = bottlenecks;
        UnusedSupply = unusedSupply;
        UnknownSupply = unknownSupply;
        Parts = parts;
        _partOf = parts.SelectMany(part => part.Buildables.Select(id => (id, part))).ToDictionary(entry => entry.id, entry => entry.part);
    }

    /// <summary>True when every part settled; false when one stopped at the limit of sweeps.</summary>
    public bool Settled => Parts.All(part => part.Settled);

    /// <summary>The most sweeps any part took.</summary>
    public int Sweeps => Parts.Count == 0 ? 0 : Parts.Max(part => part.Sweeps);

    public IReadOnlyList<FlowPart> Parts { get; }

    public IReadOnlyCollection<MachineState> Machines => _machines.Values.ToList();

    public IReadOnlyList<Bottleneck> Bottlenecks { get; }

    public IReadOnlyList<UnusedSupply> UnusedSupply { get; }

    public IReadOnlyList<UnknownSupply> UnknownSupply { get; }

    public MachineState Machine(BuildableId id) => _machines[id];

    /// <summary>The part a buildable belongs to: whether its numbers settled.</summary>
    public FlowPart PartOf(BuildableId id) => _partOf[id];

    /// <summary>
    /// What flows into a buildable over all its inputs together, per item and minute. The connections into each
    /// buildable are looked up once: asking this for every belt of a big world would otherwise walk every connection
    /// for each of them.
    /// </summary>
    public IReadOnlyDictionary<ItemId, double> Into(BuildableId id)
    {
        _incoming ??= Enumerable.Range(0, _network.Edges.Count).ToLookup(edge => _network.Edges[edge].To);
        Dictionary<ItemId, double> into = [];
        foreach (int edge in _incoming[_network.IndexOf[id]])
        {
            foreach ((ItemId item, double rate) in _flows[edge])
            {
                into[item] = into.GetValueOrDefault(item) + rate;
            }
        }

        return into;
    }

    /// <summary>What flows of an item over the connection from one buildable to another, per minute.</summary>
    public double Flow(BuildableId from, BuildableId to, ItemId item)
    {
        int fromIndex = _network.IndexOf[from];
        int toIndex = _network.IndexOf[to];
        return Enumerable.Range(0, _network.Edges.Count)
            .Where(edge => _network.Edges[edge] == (fromIndex, toIndex))
            .Sum(edge => _flows[edge].GetValueOrDefault(item));
    }
}