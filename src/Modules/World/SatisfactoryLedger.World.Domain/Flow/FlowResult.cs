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

/// <summary>The steady state of a flow network: approximations to 0.0001/min (BR-A10, ADR-0004).</summary>
public sealed class FlowResult(
    FlowNetwork network,
    IReadOnlyList<IReadOnlyDictionary<ItemId, double>> flows,
    IReadOnlyDictionary<BuildableId, MachineState> machines,
    IReadOnlyList<Bottleneck> bottlenecks,
    IReadOnlyList<UnusedSupply> unusedSupply,
    IReadOnlyList<UnknownSupply> unknownSupply,
    bool settled,
    int sweeps)
{
    /// <summary>False when the simulation stopped at its limit of sweeps without settling.</summary>
    public bool Settled { get; } = settled;

    public int Sweeps { get; } = sweeps;

    public IReadOnlyCollection<MachineState> Machines => machines.Values.ToList();

    public IReadOnlyList<Bottleneck> Bottlenecks { get; } = bottlenecks;

    public IReadOnlyList<UnusedSupply> UnusedSupply { get; } = unusedSupply;

    public IReadOnlyList<UnknownSupply> UnknownSupply { get; } = unknownSupply;

    public MachineState Machine(BuildableId id) => machines[id];

    /// <summary>What flows of an item over the connection from one buildable to another, per minute.</summary>
    public double Flow(BuildableId from, BuildableId to, ItemId item)
    {
        int fromIndex = network.IndexOf[from];
        int toIndex = network.IndexOf[to];
        return Enumerable.Range(0, network.Edges.Count)
            .Where(edge => network.Edges[edge] == (fromIndex, toIndex))
            .Sum(edge => flows[edge].GetValueOrDefault(item));
    }
}
