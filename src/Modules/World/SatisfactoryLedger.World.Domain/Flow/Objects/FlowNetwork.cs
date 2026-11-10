namespace SatisfactoryLedger.World.Domain.Flow.Objects;

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

        List<Connection> all = [.. connections];
        
        Edges = [.. all.Select(connection => (
            From: index.TryGetValue(connection.From, out int from) ? from : throw new ArgumentException($"{connection} starts outside the network", nameof(connections)),
            To: index.TryGetValue(connection.To, out int to) ? to : throw new ArgumentException($"{connection} ends outside the network", nameof(connections))))];
        IndexOf = index;
        Ports = [.. all.Select(connection => (connection.FromPort, connection.ToPort))];
    }

    public IReadOnlyList<FlowNode> Nodes { get; }

    /// <summary>Each connection by the positions of its nodes in <see cref="Nodes"/>.</summary>
    public IReadOnlyList<(int From, int To)> Edges { get; }

    public IReadOnlyDictionary<BuildableId, int> IndexOf { get; }

    public IReadOnlySet<ItemId> Fluids { get; }

    /// <summary>Per connection, as in <see cref="Edges"/>: the splitter output it leaves from and the merger input it enters, if known.</summary>
    public IReadOnlyList<(int? From, int? To)> Ports { get; }

    /// <summary>
    /// The connected parts of the network, ignoring direction, each as a network of its own: they settle on their own
    /// (BR-A10). Found with a union-find: every connection joins the parts of its two ends.
    /// </summary>
    public IReadOnlyList<NetworkPart> Parts()
    {
        int[] root = [.. Enumerable.Range(0, Nodes.Count)];
        foreach ((int from, int to) in Edges)
        {
            root[Find(from)] = Find(to);
        }

        Dictionary<int, (List<int> Nodes, List<int> Edges)> parts = [];
        for (int node = 0; node < Nodes.Count; node++)
        {
            int part = Find(node);
            if (!parts.TryGetValue(part, out (List<int> Nodes, List<int> Edges) found))
            {
                found = ([], []);
                parts[part] = found;
            }

            found.Nodes.Add(node);
        }

        for (int edge = 0; edge < Edges.Count; edge++)
        {
            parts[Find(Edges[edge].From)].Edges.Add(edge);
        }

        return
        [
            .. parts.Values.Select(part => new NetworkPart(
                new FlowNetwork(
                    part.Nodes.Select(node => Nodes[node]),
                    part.Edges.Select(edge => new Connection(Nodes[Edges[edge].From].Id, Nodes[Edges[edge].To].Id, Ports[edge].From, Ports[edge].To)),
                    Fluids),
                part.Edges))
        ];

        int Find(int node)
        {
            while (root[node] != node)
            {
                root[node] = root[root[node]];
                node = root[node];
            }

            return node;
        }
    }
}
