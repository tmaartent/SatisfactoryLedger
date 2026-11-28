using SatisfactoryLedger.World.Contracts.Analysis;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Tracing;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Analysis;

/// <summary>What flows through each pipe in the model, to show beside what's in it (asked 2026-11-24).</summary>
internal static class PipeFlows
{
    /// <summary>
    /// Per pipe that's drawn, with a curve: its own flow when it has one, else its network's. Pipes of a network that
    /// takes in nothing are left out.
    /// </summary>
    public static List<PipeFlowFinding> Of(SnapshotDocument snapshot, FlowReadings readings)
    {
        Dictionary<string, BuildableDocument> byId = snapshot.Buildables.ToDictionary(buildable => buildable.Id, StringComparer.Ordinal);
        Dictionary<string, string> hubOf = HubsOf(snapshot, byId);
        Dictionary<string, int> capacities = snapshot.TransportTiers.ToDictionary(tier => tier.Id, tier => tier.CapacityPerMinute, StringComparer.Ordinal);
        double? CapacityOf(BuildableDocument pipe) => capacities.TryGetValue(pipe.ClassName, out int capacity) ? capacity : null;
        List<PipeFlowFinding> flows = [];
        foreach (BuildableDocument pipe in snapshot.Buildables.Where(buildable => buildable is { Kind: LogisticsKind.Pipe, Curve.Count: > 0 }))
        {
            // A hub isn't a conduit of its own: it takes in what the pipes and buffers feeding it carry.
            if (readings.Carrying(BuildableId.Of(pipe.Id)) is { Count: > 0 } own)
            {
                flows.Add(new PipeFlowFinding(pipe.Id, Rounded(own), OfNetwork: false, CapacityOf(pipe)));
            }
            else if (hubOf.TryGetValue(pipe.Id, out string? hub) && readings.Carrying(BuildableId.Of(hub)) is { Count: > 0 } network)
            {
                flows.Add(new PipeFlowFinding(pipe.Id, Rounded(network), OfNetwork: true, CapacityOf(pipe)));
            }
        }

        return flows;
    }

    /// <summary>
    /// Per piece of a pipe network, its hub (BR-A6): the hub is what the pipes at the network's edge connect to.
    /// </summary>
    private static Dictionary<string, string> HubsOf(SnapshotDocument snapshot, Dictionary<string, BuildableDocument> byId)
    {
        Dictionary<string, string> hubNextTo = [];
        foreach (ConnectionDocument connection in snapshot.Connections)
        {
            if (byId.TryGetValue(connection.To, out BuildableDocument? to) && to.Kind == LogisticsKind.PipeNetwork)
            {
                hubNextTo[connection.From] = connection.To;
            }
            else if (byId.TryGetValue(connection.From, out BuildableDocument? from) && from.Kind == LogisticsKind.PipeNetwork)
            {
                hubNextTo[connection.To] = connection.From;
            }
        }

        Dictionary<string, IReadOnlySet<string>> networkOf = snapshot.PipeNetworks()
            .SelectMany(network => network.Select(piece => (Piece: piece, Network: network)))
            .ToDictionary(entry => entry.Piece, entry => entry.Network, StringComparer.Ordinal);
        Dictionary<string, string> hubOf = [];
        foreach ((string edge, string hub) in hubNextTo)
        {
            // The first edge found of a network names its hub. This relies on a Dictionary enumerating in insertion order
            // (the order of the connections), which holds while nothing is removed but isn't promised (primer).
            if (!hubOf.ContainsKey(edge) && networkOf.TryGetValue(edge, out IReadOnlySet<string>? network))
            {
                foreach (string piece in network)
                {
                    hubOf[piece] = hub;
                }
            }
        }

        return hubOf;
    }

    private static Dictionary<string, double> Rounded(IReadOnlyDictionary<ItemId, double> flows) =>
        flows.OrderBy(entry => entry.Key.Id, StringComparer.Ordinal).ToDictionary(entry => entry.Key.Id, entry => SnapshotAnalysis.Round(entry.Value));
}
