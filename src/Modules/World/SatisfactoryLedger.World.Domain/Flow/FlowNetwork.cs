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
public sealed record FlowUnknownSource(BuildableId Id, IReadOnlyDictionary<ItemId, double> Stock) : FlowNode(Id);

/// <summary>Takes everything it's offered: a consumer, or a container at the end of a line (BR-A7, BR-A8).</summary>
public sealed record FlowSink(BuildableId Id) : FlowNode(Id);

/// <summary>A belt, lift or pipe, or a container inside a line: passes items on, at most <paramref name="Capacity"/> per minute (BR-A3).</summary>
public sealed record FlowConduit(BuildableId Id, double Capacity) : FlowNode(Id);

/// <summary>Divides what comes in evenly over its outputs that take items (BR-A4).</summary>
public sealed record FlowSplitter(BuildableId Id) : FlowNode(Id);

/// <summary>Takes evenly from its inputs, up to what its output can carry (BR-A5).</summary>
public sealed record FlowMerger(BuildableId Id) : FlowNode(Id);

/// <summary>The nodes and their connections, ready to simulate.</summary>
public sealed class FlowNetwork
{
    public FlowNetwork(IEnumerable<FlowNode> nodes, IEnumerable<Connection> connections)
    {
        Nodes = nodes.ToList();
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
}
