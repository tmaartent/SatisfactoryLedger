using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.Domain.Flow.Objects;

namespace SatisfactoryLedger.World.Simulation;

public sealed record MergerDocument(string Id, Carries Carries, IReadOnlyList<int>? Priorities = null) : FlowNodeDocument(Id)
{
    public override FlowNode ToNode() => new FlowMerger(BuildableId.Of(Id), Carries, Priorities);
}