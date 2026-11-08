namespace SatisfactoryLedger.World.Domain.Flow;

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
}