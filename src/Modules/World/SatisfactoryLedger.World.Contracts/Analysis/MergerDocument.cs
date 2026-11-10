namespace SatisfactoryLedger.World.Contracts.Analysis;

public sealed record MergerDocument(string Id, Carries Carries, IReadOnlyList<int>? Priorities = null) : FlowNodeDocument(Id);