namespace SatisfactoryLedger.World.Domain.Flow;

/// <summary>Why a machine runs below 100% (BR-A11).</summary>
public enum MachineLimit
{
    /// <summary>It runs at 100%.</summary>
    None,

    /// <summary>It gets less of an input item than it asks.</summary>
    Starved,

    /// <summary>An output item can't (all) leave.</summary>
    OutputBlocked,

    /// <summary>It has no recipe the analysis knows, so it asks and makes nothing.</summary>
    NoRecipe,
}

/// <param name="Utilisation">Between 0 and 1 (BR-A1).</param>
/// <param name="LimitingItem">The item it's starved of or blocked on; null when <see cref="Limit"/> is None or NoRecipe.</param>
public sealed record MachineState(BuildableId Machine, double Utilisation, MachineLimit Limit, ItemId? LimitingItem);

/// <summary>A belt, lift or pipe at its capacity while more is offered upstream of it (BR-A12).</summary>
public sealed record Bottleneck(BuildableId Conduit, double Capacity, double Flow, double OfferedMore);

/// <summary>What a provider could deliver on top of what it does (BR-A13).</summary>
public sealed record UnusedSupply(BuildableId Provider, ItemId Item, double Offered, double Delivered);

/// <param name="StockMinutes">How long its current stock lasts at what it delivers; null when its stock doesn't hold what it delivers.</param>
/// <param name="Confirmed">True when the stock lasts at least 30 minutes (BR-A9).</param>
public sealed record UnknownSupply(BuildableId Source, IReadOnlyDictionary<ItemId, double> Delivered, double? StockMinutes, bool Confirmed);

/// <summary>
/// A connected part of the world, simulated on its own. When it didn't settle within the limit of sweeps, its numbers
/// are approximate: it came close, but some flows still moved by more than 0.0001 a minute (BR-A10).
/// </summary>
public sealed record FlowPart(IReadOnlyList<BuildableId> Buildables, bool Settled, int Sweeps);

/// <summary>The steady state of a flow network: approximations to 0.0001/min (BR-A10, ADR-0004).</summary>
public sealed class FlowResult
{
    private readonly FlowNetwork _network;
    private readonly IReadOnlyList<IReadOnlyDictionary<ItemId, double>> _flows;
    private readonly IReadOnlyDictionary<BuildableId, MachineState> _machines;
    private readonly Dictionary<BuildableId, FlowPart> _partOf;

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

    /// <summary>What flows into a buildable over all its inputs together, per item and minute.</summary>
    public IReadOnlyDictionary<ItemId, double> Into(BuildableId id)
    {
        int index = _network.IndexOf[id];
        Dictionary<ItemId, double> into = [];
        for (int edge = 0; edge < _network.Edges.Count; edge++)
        {
            if (_network.Edges[edge].To == index)
            {
                foreach ((ItemId item, double rate) in _flows[edge])
                {
                    into[item] = into.GetValueOrDefault(item) + rate;
                }
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
