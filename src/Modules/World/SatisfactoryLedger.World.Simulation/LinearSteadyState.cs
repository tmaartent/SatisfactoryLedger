using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow.Objects;

namespace SatisfactoryLedger.World.Simulation;

/// <summary>
/// The steady state as a sequence of linear programmes (ADR-0026), solved with OR-Tools' GLOP.
/// <para>
/// The game's splitters and mergers share evenly each on their own (BR-A4, A5), which no single objective expresses.
/// So each round guesses which shares are <i>capped</i>: held below their group's level by what lies beyond them. The
/// rest carry exactly the level. A round first maximises what the sources push in (<see cref="LinearProgramme.Push"/>);
/// a share whose level holds the sources back is capped for the next round. When no share holds them back, the
/// capped shares are filled as far as they go, and then every machine runs as fast as it can.
/// </para>
/// Shares are only ever capped, never uncapped again, so the sequence ends within as many rounds as there are shares.
/// </summary>
internal static class LinearSteadyState
{
    public static LinearFlow Solve(FlowNetwork network)
    {
        ArgumentNullException.ThrowIfNull(network);

        ItemsOnConnections items = ItemsOnConnections.Of(network);
        List<SharingGroup> groups = SharingGroups(network, items);
        HashSet<(int Group, int Edge)> capped = [];
        int shares = groups.Sum(group => group.Edges.Count);

        for (int round = 0; ; round++)
        {
            using var programme = new LinearProgramme(network, items, groups, capped);
            double pushed = programme.Maximise(programme.Push);
            List<(int Group, int Edge)> holdingBack = [.. programme.SharesHoldingBack()];
            if (holdingBack.Count == 0 || round >= shares)
            {
                programme.Keep(programme.Push, pushed);
                programme.Keep(programme.CappedShares, programme.Maximise(programme.CappedShares));
                programme.Maximise(programme.Utilisation);
                return programme.Result();
            }

            capped.UnionWith(holdingBack);
        }
    }

    /// <summary>
    /// The connections that share evenly (BR-A4, A5): a splitter's outputs; the inputs of a splitter, merger or belt fed
    /// by more than one connection; and per item, the belts that bring a machine that item.
    /// </summary>
    private static List<SharingGroup> SharingGroups(FlowNetwork network, ItemsOnConnections items)
    {
        List<int>[] into = [.. network.Nodes.Select(_ => new List<int>())];
        List<int>[] outOf = [.. network.Nodes.Select(_ => new List<int>())];
        for (int edge = 0; edge < network.Edges.Count; edge++)
        {
            into[network.Edges[edge].To].Add(edge);
            outOf[network.Edges[edge].From].Add(edge);
        }

        List<SharingGroup> groups = [];
        for (int node = 0; node < network.Nodes.Count; node++)
        {
            switch (network.Nodes[node])
            {
                case FlowSplitter:
                    AddIfShared(outOf[node], null);
                    AddIfShared(into[node], null);
                    break;
                case FlowMerger or FlowConduit:
                    AddIfShared(into[node], null);
                    break;
                case FlowMachine machine:
                    foreach (ItemId item in machine.Demand.Keys)
                    {
                        AddIfShared([.. into[node].Where(edge => items.Items[edge].Contains(item))], item);
                    }

                    break;
            }
        }

        return groups;

        void AddIfShared(List<int> edges, ItemId? item)
        {
            if (edges.Count > 1)
            {
                groups.Add(new SharingGroup(edges, item));
            }
        }
    }
}
