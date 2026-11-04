using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.SaveFile;

/// <summary>
/// One end of a link, as the save stores it: the port <paramref name="Path"/> of <paramref name="Owner"/>, linked to
/// the port <paramref name="LinkedTo"/> of another buildable, or to nothing.
/// </summary>
internal sealed record Connector(BuildableId Owner, string Path, Port Port, string? LinkedTo);

/// <summary>Turns the save's links into connections with a direction.</summary>
internal static class ConnectionBuilder
{
    /// <param name="inWorld">The buildables in the world. Links to anything else (an unsupported station) are left out.</param>
    public static IReadOnlyList<Connection> Build(IReadOnlyCollection<Connector> connectors, IReadOnlySet<BuildableId> inWorld)
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
                    directed.Add(new Connection(one.Owner, other.Owner));
                    break;
                case (Port.In, Port.Out or Port.Undirected):
                case (Port.Undirected, Port.Out):
                    directed.Add(new Connection(other.Owner, one.Owner));
                    break;
                case (Port.Undirected, Port.Undirected):
                    undirected.Add((one.Owner, other.Owner));
                    break;
                default:
                    throw new SaveFileException($"{one.Path} and {other.Path} are both {one.Port.ToString().ToLowerInvariant()}puts");
            }
        }

        return [.. directed, .. OrientAwayFromSources(directed, undirected)];
    }

    /// <summary>
    /// Pipes have no direction of their own. Fluid flows away from where it enters the pipe network (an extractor or
    /// a machine's output), so walk the network outward from those points, breadth first.
    /// A pipe that no source reaches gets no direction, and no connection.
    /// </summary>
    private static List<Connection> OrientAwayFromSources(List<Connection> directed, List<(BuildableId A, BuildableId B)> undirected)
    {
        ILookup<BuildableId, BuildableId> neighbours = undirected
            .SelectMany(edge => new[] { (From: edge.A, To: edge.B), (From: edge.B, To: edge.A) })
            .ToLookup(edge => edge.From, edge => edge.To);

        Dictionary<BuildableId, int> depth = directed
            .Select(connection => connection.To)
            .Where(neighbours.Contains)
            .Distinct()
            .ToDictionary(fed => fed, _ => 0);
        Queue<BuildableId> toVisit = new(depth.Keys);

        while (toVisit.TryDequeue(out BuildableId? current))
        {
            foreach (BuildableId next in neighbours[current].Where(next => depth.TryAdd(next, depth[current] + 1)))
            {
                toVisit.Enqueue(next);
            }
        }

        List<Connection> oriented = [];
        foreach ((BuildableId a, BuildableId b) in undirected)
        {
            if (depth.TryGetValue(a, out int depthA) && depth.TryGetValue(b, out int depthB) && depthA != depthB)
            {
                oriented.Add(depthA < depthB ? new Connection(a, b) : new Connection(b, a));
            }
        }

        return oriented;
    }
}
