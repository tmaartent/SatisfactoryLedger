using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;

namespace SatisfactoryLedger.World.Simulation;

public sealed record SplitterDocument(string Id, Carries Carries, IReadOnlyList<SortRuleDocument>? Rules = null) : FlowNodeDocument(Id)
{
    public override FlowNode ToNode() => new FlowSplitter(BuildableId.Of(Id), Carries,
        Rules?.Select(rule => new SortRule(rule.Output, rule.Kind, rule.Item is null ? null : ItemId.Of(rule.Item))).ToList());
}