namespace SatisfactoryLedger.World.Contracts.Analysis;

public sealed record SplitterDocument(string Id, Carries Carries, IReadOnlyList<SortRuleDocument>? Rules = null) : FlowNodeDocument(Id);