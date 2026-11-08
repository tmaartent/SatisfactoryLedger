namespace SatisfactoryLedger.World.Domain.Flow;

/// <summary>Divides what comes in evenly over its outputs that take items (BR-A4). A pipe junction is one too (BR-A6).</summary>
/// <param name="Rules">A smart or programmable splitter's rules per output; null or empty for a plain splitter.</param>
public sealed record FlowSplitter(BuildableId Id, Carries Carries = Carries.Anything, IReadOnlyList<SortRule>? Rules = null) : FlowNode(Id);