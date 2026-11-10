using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow.Objects;

namespace SatisfactoryLedger.World.Simulation;

/// <summary>
/// BR-A8: the connections that lead only to what takes the line's leftovers: a dimensional depot uploader, or the fuel of
/// a station whose burn rate isn't known. Along logistics, every way on must end in one.
/// </summary>
internal sealed class LeftoversOnly(FlowNetwork network, ItemsOnConnections items, List<int>[] outOf)
{
    private readonly Dictionary<int, bool> _known = [];

    public bool Of(int edge)
    {
        if (_known.TryGetValue(edge, out bool known))
        {
            return known;
        }

        // A loop of logistics leads nowhere new: counted as not leftovers while it's being looked at.
        _known[edge] = false;
        int to = network.Edges[edge].To;
        bool leftovers = network.Nodes[to] switch
        {
            FlowSink { LeftoversOnly: true } => true,
            FlowSink { BurnRate: null, Burns: { Count: > 0 } fuels } => BringsOnly(edge, fuels),
            FlowUnknownSource { BurnRate: null, Burns: { Count: > 0 } fuels } => BringsOnly(edge, fuels),
            FlowConduit or FlowSplitter or FlowMerger => outOf[to].Count > 0 && outOf[to].All(Of),
            _ => false,
        };
        _known[edge] = leftovers;
        return leftovers;
    }

    private bool BringsOnly(int edge, IReadOnlySet<ItemId> fuels) => items.Items[edge].Count > 0 && items.Items[edge].All(fuels.Contains);
}
