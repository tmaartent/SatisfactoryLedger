using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.Domain.Flow.Objects;

namespace SatisfactoryLedger.World.Simulation;

/// <param name="BurnRate">For a drone port that loads: its fuel per minute; null when that isn't known (BR-A16).</param>
public sealed record SinkDocument(string Id, IReadOnlyList<string>? Burns = null, bool LeftoversOnly = false, double? BurnRate = null) : FlowNodeDocument(Id)
{
    public override FlowNode ToNode() => new FlowSink(BuildableId.Of(Id), Items(Burns), LeftoversOnly, BurnRate);
}