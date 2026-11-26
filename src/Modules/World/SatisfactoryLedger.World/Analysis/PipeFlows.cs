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
    /// Per piece of a pipe network, its hub (BR-A6): the pieces that touch make one network, and its hub is what the
    /// pipes at its edge connect to.
    /// </summary>
    private static Dictionary<string, string> HubsOf(SnapshotDocument snapshot, Dictionary<string, BuildableDocument> byId)
    {
        ILookup<string, string> touching = snapshot.PipeNeighbours();
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

        Dictionary<string, string> hubOf = [];
        HashSet<string> seen = [];
        foreach (string start in hubNextTo.Keys)
        {
            if (!seen.Add(start))
            {
                continue;
            }

            List<string> network = [start];
            for (int next = 0; next < network.Count; next++)
            {
                network.AddRange(touching[network[next]].Where(seen.Add));
            }

            network.ForEach(piece => hubOf[piece] = hubNextTo[start]);
        }

        return hubOf;
    }

    private static Dictionary<string, double> Rounded(IReadOnlyDictionary<ItemId, double> flows) =>
        flows.OrderBy(entry => entry.Key.Id, StringComparer.Ordinal).ToDictionary(entry => entry.Key.Id, entry => SnapshotAnalysis.Round(entry.Value));
}
