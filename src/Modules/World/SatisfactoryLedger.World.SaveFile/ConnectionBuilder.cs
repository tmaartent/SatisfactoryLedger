using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.SaveFile;

/// <summary>
/// One end of a link, as the save stores it: the port <paramref name="Path"/> of <paramref name="Owner"/>, linked to
/// the port <paramref name="LinkedTo"/> of another buildable, or to nothing.
/// </summary>
/// <param name="Index">Which output of a splitter or input of a merger this is, counted from 0; null elsewhere.</param>
internal sealed record Connector(BuildableId Owner, string Path, Port Port, string? LinkedTo, int? Index = null);

/// <summary>The save's links as connections with a direction, and the pipe networks they made into hubs.</summary>
internal sealed record Wiring(IReadOnlyList<Connection> Connections, IReadOnlyList<PipeNetwork> PipeNetworks);

/// <summary>
/// A connected pipe network (BR-A6): one hub that takes from every output feeding it and shares over every input it
/// feeds, as the game treats a pipe network as one. <see cref="Pipes"/> are its pipes, junctions, pumps, valves and
/// fluid buffers.
/// </summary>
internal sealed record PipeNetwork(BuildableId Hub, IReadOnlyList<BuildableId> Pipes)
{
    /// <summary>The hub's id: named after the network's pipe whose id sorts first, so it stays the same from save to save.</summary>
    public static BuildableId HubOf(BuildableId firstPipe) => BuildableId.Of($"PipeNetwork:{firstPipe.Id}");
}

/// <summary>Turns the save's links into connections with a direction.</summary>
internal static class ConnectionBuilder
{
    /// <param name="inWorld">The buildables in the world. Links to anything else (an unsupported station) are left out.</param>
    /// <param name="fluidBuffers">The fluid buffers in the world: in a pipe network, a buffer is no pipe (see <see cref="Networks"/>).</param>
    public static Wiring Build(IReadOnlyCollection<Connector> connectors, IReadOnlySet<BuildableId> inWorld, IReadOnlySet<BuildableId> fluidBuffers)
    {
        Dictionary<string, Connector> byPath = connectors.ToDictionary(connector => connector.Path, StringComparer.Ordinal);
        List<Connection> directed = [];
        List<(BuildableId A, BuildableId B)> undirected = [];

        foreach (Connector one in connectors.Where(connector => connector.LinkedTo is not null))
        {
            Connector other = byPath.GetValueOrDefault(one.LinkedTo!)
                ?? throw new SaveFileException($"{one.Path} is linked to {one.LinkedTo}, which is not in the save");
            if (other.LinkedTo != one.Path)
            {
                throw new SaveFileException($"{one.Path} is linked to {other.Path}, but that link is stored on one end only");
            }

            // Each link is stored on both ends: handle it once, from the end that sorts first.
            if (string.CompareOrdinal(one.Path, other.Path) > 0 || !inWorld.Contains(one.Owner) || !inWorld.Contains(other.Owner))
            {
                continue;
            }

            switch (one.Port, other.Port)
            {
                case (Port.Out, Port.In or Port.Undirected):
                case (Port.Undirected, Port.In):
                    directed.Add(new Connection(one.Owner, other.Owner, one.Index, other.Index));
                    break;
                case (Port.In, Port.Out or Port.Undirected):
                case (Port.Undirected, Port.Out):
                    directed.Add(new Connection(other.Owner, one.Owner, other.Index, one.Index));
                    break;
                case (Port.Undirected, Port.Undirected):
                    undirected.Add((one.Owner, other.Owner));
                    break;
                default:
                    throw new SaveFileException($"{one.Path} and {other.Path} are both {one.Port.ToString().ToLowerInvariant()}puts");
            }
        }

        return Networks(directed, undirected, fluidBuffers);
    }

    /// <summary>
    /// Pipes have no direction of their own (decided 2026-11-07): each connected set of pipe links becomes one hub.
    /// <list type="bullet">
    /// <item>A pipe an output feeds sends into the hub; a pipe that feeds an input gets from the hub. Those pipes stay in
    /// the flow, so their capacity still counts. A pipe that does both sends into the hub, and its input gets its share
    /// straight from the hub.</item>
    /// <item>The pipes inside a network aren't connected: they no longer carry anything of their own.</item>
    /// <item>A fluid buffer in a network something feeds is full in steady state: it passes nothing. In a network nothing
    /// else feeds, it feeds the hub (BR-A9).</item>
    /// </list>
    /// </summary>
    private static Wiring Networks(List<Connection> directed, List<(BuildableId A, BuildableId B)> undirected, IReadOnlySet<BuildableId> fluidBuffers)
    {
        Dictionary<BuildableId, BuildableId> root = [];
        BuildableId Find(BuildableId id)
        {
            while (root.TryGetValue(id, out BuildableId? parent) && parent != id)
            {
                id = parent;
            }

            return id;
        }

        foreach ((BuildableId a, BuildableId b) in undirected)
        {
            root.TryAdd(a, a);
            root.TryAdd(b, b);
            (BuildableId ra, BuildableId rb) = (Find(a), Find(b));
            if (ra != rb)
            {
                root[ra] = rb;
            }
        }

        List<PipeNetwork> networks = [];
        Dictionary<BuildableId, BuildableId> hubOf = [];
        foreach (IGrouping<BuildableId, BuildableId> members in root.Keys.GroupBy(Find))
        {
            List<BuildableId> pipes = [.. members.OrderBy(id => id.Id, StringComparer.Ordinal)];
            BuildableId hub = PipeNetwork.HubOf(pipes[0]);
            networks.Add(new PipeNetwork(hub, pipes));
            pipes.ForEach(pipe => hubOf[pipe] = hub);
        }

        HashSet<BuildableId> fed = [.. directed.Where(connection => hubOf.ContainsKey(connection.To)).Select(connection => connection.To)];
        HashSet<BuildableId> fedHubs = [.. fed.Select(pipe => hubOf[pipe])];
        List<Connection> connections = [];
        foreach (Connection connection in directed)
        {
            if (hubOf.TryGetValue(connection.From, out BuildableId? hub) && fed.Contains(connection.From))
            {
                // A pipe an output feeds sends everything into the hub; its input gets its share from there.
                connections.Add(connection with { From = hub, FromPort = null });
            }
            else if (hubOf.TryGetValue(connection.From, out hub))
            {
                connections.Add(connection);
                connections.Add(new Connection(hub, connection.From));
            }
            else
            {
                connections.Add(connection);
            }
        }

        connections.AddRange(fed.Select(pipe => new Connection(pipe, hubOf[pipe])));
        connections.AddRange(networks
            .Where(network => !fedHubs.Contains(network.Hub))
            .SelectMany(network => network.Pipes.Where(fluidBuffers.Contains).Select(buffer => new Connection(buffer, network.Hub))));

        return new Wiring([.. connections.Distinct()], networks);
    }
}
