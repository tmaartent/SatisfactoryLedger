namespace SatisfactoryLedger.World.Domain.Flow;

/// <summary>A node of the flow network: a buildable with what it asks, offers or carries per minute (BR-A1 to BR-A9).</summary>
public abstract record FlowNode(BuildableId Id);

/// <param name="Demand">Per input item, what it asks per minute at 100% utilisation: recipe rate × clock speed.</param>
/// <param name="Production">Per output item, what it makes per minute at 100%: recipe rate × clock speed × production boost.</param>
public sealed record FlowMachine(BuildableId Id, IReadOnlyDictionary<ItemId, double> Demand, IReadOnlyDictionary<ItemId, double> Production)
    : FlowNode(Id);

/// <param name="Offer">What it can deliver per minute (BR-A2).</param>
public sealed record FlowProvider(BuildableId Id, ItemId Item, double Offer) : FlowNode(Id);

/// <summary>A source whose rate isn't known, such as a station or a container nothing feeds (BR-A9).</summary>
/// <param name="Stock">What it holds right now, per item. Decides between assumed and confirmed supplied.</param>
/// <param name="Burns">
/// The fuel it burns, for a drone port or truck station: fuel coming in takes only what the line's other consumers
/// leave. Everything else coming in, it takes like a consumer.
/// </param>
public sealed record FlowUnknownSource(
    BuildableId Id,
    IReadOnlyDictionary<ItemId, double> Stock,
    Carries Carries = Carries.Anything,
    IReadOnlySet<ItemId>? Burns = null) : FlowNode(Id);

/// <summary>Takes everything it's offered: a consumer, or a container at the end of a line (BR-A7, BR-A8).</summary>
/// <param name="Burns">For a station that loads: the fuel it burns, which takes only what the others leave.</param>
public sealed record FlowSink(BuildableId Id, IReadOnlySet<ItemId>? Burns = null) : FlowNode(Id);

/// <summary>A belt, lift or pipe, or a container inside a line: passes items on, at most <paramref name="Capacity"/> per minute (BR-A3).</summary>
public sealed record FlowConduit(BuildableId Id, double Capacity, Carries Carries = Carries.Anything) : FlowNode(Id);

/// <summary>Divides what comes in evenly over its outputs that take items (BR-A4). A pipe junction is one too (BR-A6).</summary>
public sealed record FlowSplitter(BuildableId Id, Carries Carries = Carries.Anything) : FlowNode(Id);

/// <summary>Takes evenly from its inputs, up to what its output can carry (BR-A5).</summary>
public sealed record FlowMerger(BuildableId Id, Carries Carries = Carries.Anything) : FlowNode(Id);

/// <summary>What a node can move: belts, splitters, mergers and containers move solids; pipes, junctions and fluid buffers fluids.</summary>
public enum Carries
{
    /// <summary>Not limited by the node itself: a machine, a sink, a station. Its connection's other end decides.</summary>
    Anything,
    Solids,
    Fluids,
}

/// <summary>The nodes and their connections, ready to simulate.</summary>
public sealed class FlowNetwork
{
    /// <param name="fluids">The items that are fluids: they go over pipes. Every other item is a solid and goes over belts.</param>
    public FlowNetwork(IEnumerable<FlowNode> nodes, IEnumerable<Connection> connections, IEnumerable<ItemId>? fluids = null)
    {
        Nodes = nodes.ToList();
        Fluids = (fluids ?? []).ToHashSet();
        Dictionary<BuildableId, int> index = [];
        for (int i = 0; i < Nodes.Count; i++)
        {
            if (!index.TryAdd(Nodes[i].Id, i))
            {
                throw new ArgumentException($"{Nodes[i].Id} is in the network more than once", nameof(nodes));
            }
        }

        Edges = [.. connections.Select(connection => (
            From: index.TryGetValue(connection.From, out int from) ? from : throw new ArgumentException($"{connection} starts outside the network", nameof(connections)),
            To: index.TryGetValue(connection.To, out int to) ? to : throw new ArgumentException($"{connection} ends outside the network", nameof(connections))))];
        IndexOf = index;
    }

    public IReadOnlyList<FlowNode> Nodes { get; }

    /// <summary>Each connection by the positions of its nodes in <see cref="Nodes"/>.</summary>
    public IReadOnlyList<(int From, int To)> Edges { get; }

    public IReadOnlyDictionary<BuildableId, int> IndexOf { get; }

    public IReadOnlySet<ItemId> Fluids { get; }
}
