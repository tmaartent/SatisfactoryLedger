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
                foreach (var tier in programme.CappedShares)
                {
                    programme.Keep(tier, programme.Maximise(tier));
                }

                programme.Maximise(programme.Utilisation);
                return programme.Result();
            }

            capped.UnionWith(holdingBack);
        }
    }

    /// <summary>
    /// The connections that share evenly (BR-A4, A5), in tiers where one waits for another:
    /// <list type="bullet">
    /// <item>a splitter's outputs, per group of items its rules send alike: the main outputs, then the overflow ones,
    /// then the same two into what takes only leftovers (an uploader, a station's fuel; BR-A8);</item>
    /// <item>a merger's inputs, per priority, the highest first;</item>
    /// <item>the inputs of a splitter or belt fed by more than one connection;</item>
    /// <item>per item, the belts that bring a machine that item.</item>
    /// </list>
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

        var leftovers = new LeftoversOnly(network, items, outOf);
        List<SharingGroup> groups = [];
        for (int node = 0; node < network.Nodes.Count; node++)
        {
            switch (network.Nodes[node])
            {
                case FlowSplitter { Rules.Count: > 0 } sorting:
                    var rules = new SortRules(network, sorting);
                    IEnumerable<IGrouping<string, ItemId>> alike = into[node].SelectMany(edge => items.Items[edge]).Distinct()
                        .GroupBy(item => string.Concat(outOf[node].Select(edge => rules.Takes(edge, item) ? 't' : rules.Overflows(edge, item) ? 'o' : '-')));
                    foreach (IGrouping<string, ItemId> sent in alike)
                    {
                        HashSet<ItemId> sentItems = [.. sent];
                        ItemId first = sent.First();
                        List<int> main = [.. outOf[node].Where(edge => rules.Takes(edge, first))];
                        List<int> overflow = [.. outOf[node].Where(edge => rules.Overflows(edge, first))];
                        AddTiers(sentItems, [.. main.Where(edge => !leftovers.Of(edge))], [.. overflow.Where(edge => !leftovers.Of(edge))],
                            [.. main.Where(leftovers.Of)], [.. overflow.Where(leftovers.Of)]);
                    }

                    AddIfShared(into[node], null);
                    break;
                case FlowSplitter:
                    AddTiers(null, [.. outOf[node].Where(edge => !leftovers.Of(edge))], [.. outOf[node].Where(leftovers.Of)]);
                    AddIfShared(into[node], null);
                    break;
                case FlowMerger { Priorities.Count: > 0 } merger:
                    int PriorityOf(int edge) => network.Ports[edge].To is int port && port < merger.Priorities!.Count ? merger.Priorities[port] : 0;
                    AddTiers(null, [.. into[node].GroupBy(PriorityOf).OrderByDescending(priority => priority.Key).Select(priority => priority.ToList())]);
                    break;
                case FlowMerger or FlowConduit:
                    AddIfShared(into[node], null);
                    break;
                case FlowMachine machine:
                    foreach (ItemId item in machine.Demand.Keys)
                    {
                        AddIfShared([.. into[node].Where(edge => items.Items[edge].Contains(item))], item is null ? null : new HashSet<ItemId> { item });
                    }

                    break;
            }
        }

        return groups;

        void AddIfShared(List<int> edges, IReadOnlySet<ItemId>? shared)
        {
            if (edges.Count > 1)
            {
                groups.Add(new SharingGroup(edges, shared));
            }
        }

        // Tiers in the order they're served. A single tier is an ordinary group; empty tiers are skipped.
        void AddTiers(IReadOnlySet<ItemId>? shared, params List<int>[] tiers)
        {
            List<List<int>> served = [.. tiers.Where(tier => tier.Count > 0)];
            if (served.Count == 1)
            {
                AddIfShared(served[0], shared);
                return;
            }

            List<int> above = [];
            for (int tier = 0; tier < served.Count; tier++)
            {
                groups.Add(new SharingGroup(served[tier], shared, tier, [.. above]));
                above.Add(groups.Count - 1);
            }
        }
    }
}
