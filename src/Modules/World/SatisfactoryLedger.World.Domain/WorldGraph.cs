namespace SatisfactoryLedger.World.Domain;

/// <summary>The buildables of the world and how they're connected, as read from one save. Never changed after import.</summary>
public sealed class WorldGraph
{
    private readonly Dictionary<BuildableId, Buildable> _buildables = [];
    private readonly ILookup<BuildableId, Buildable> _upstream;
    private readonly ILookup<BuildableId, Buildable> _downstream;

    public WorldGraph(IEnumerable<Buildable> buildables, IEnumerable<Connection> connections)
    {
        foreach (Buildable buildable in buildables)
        {
            if (!_buildables.TryAdd(buildable.Id, buildable))
            {
                throw new ArgumentException($"{buildable.Id} is in the world more than once", nameof(buildables));
            }
        }

        List<Connection> all = connections.ToList();
        Connections = all;
        foreach (Connection connection in all)
        {
            if (!_buildables.ContainsKey(connection.From) || !_buildables.ContainsKey(connection.To))
            {
                throw new ArgumentException($"{connection} connects a buildable that is not in the world", nameof(connections));
            }
        }

        _upstream = all.ToLookup(connection => connection.To, connection => _buildables[connection.From]);
        _downstream = all.ToLookup(connection => connection.From, connection => _buildables[connection.To]);
    }

    public IEnumerable<Buildable> Buildables => _buildables.Values;

    /// <summary>Every connection, in the direction items flow.</summary>
    public IReadOnlyList<Connection> Connections { get; }

    public Buildable? Find(BuildableId id) => _buildables.GetValueOrDefault(id);

    /// <summary>What feeds this buildable.</summary>
    public IEnumerable<Buildable> Upstream(BuildableId id) => _upstream[id];

    /// <summary>What this buildable feeds.</summary>
    public IEnumerable<Buildable> Downstream(BuildableId id) => _downstream[id];
}
