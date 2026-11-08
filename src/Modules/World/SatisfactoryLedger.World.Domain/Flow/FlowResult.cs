namespace SatisfactoryLedger.World.Domain.Flow;

/// <summary>The steady state of a flow network: approximations to 0.0001/min (BR-A10, ADR-0004).</summary>
public sealed class FlowResult
{
    private ILookup<BuildableId, ConnectionFlow>? _into;

    /// <param name="flows">Per connection of <paramref name="network"/>, in its order: what flows over it.</param>
    public FlowResult(
        FlowNetwork network,
        IReadOnlyList<IReadOnlyDictionary<ItemId, double>> flows,
        IReadOnlyDictionary<BuildableId, MachineState> machines,
        IReadOnlyList<Bottleneck> bottlenecks,
        IReadOnlyList<UnusedSupply> unusedSupply,
        IReadOnlyList<UnknownSupply> unknownSupply,
        IReadOnlyList<FlowPart> parts)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(flows);
        ArgumentNullException.ThrowIfNull(machines);

        Connections = [.. network.Edges.Select((edge, k) => new ConnectionFlow(network.Nodes[edge.From].Id, network.Nodes[edge.To].Id, flows[k]))];
        Machines = [.. machines.Values];
        Bottlenecks = bottlenecks;
        UnusedSupply = unusedSupply;
        UnknownSupply = unknownSupply;
        Parts = parts;
    }

    /// <summary>Every connection, with what flows over it.</summary>
    public IReadOnlyList<ConnectionFlow> Connections { get; }

    public IReadOnlyList<FlowPart> Parts { get; }

    public IReadOnlyCollection<MachineState> Machines { get; }

    public IReadOnlyList<Bottleneck> Bottlenecks { get; }

    public IReadOnlyList<UnusedSupply> UnusedSupply { get; }

    public IReadOnlyList<UnknownSupply> UnknownSupply { get; }

    /// <summary>
    /// What flows into a buildable over all its inputs together, per item and minute. The connections into each
    /// buildable are looked up once: asking this for every belt of a big world would otherwise walk every connection
    /// for each of them.
    /// </summary>
    public IReadOnlyDictionary<ItemId, double> Into(BuildableId id)
    {
        _into ??= Connections.ToLookup(connection => connection.To);
        Dictionary<ItemId, double> into = [];
        foreach (ConnectionFlow connection in _into[id])
        {
            foreach ((ItemId item, double rate) in connection.Items)
            {
                into[item] = into.GetValueOrDefault(item) + rate;
            }
        }

        return into;
    }
}
