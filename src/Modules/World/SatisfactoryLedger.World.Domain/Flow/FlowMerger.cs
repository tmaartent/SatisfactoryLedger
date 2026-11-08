namespace SatisfactoryLedger.World.Domain.Flow;

/// <summary>Takes evenly from its inputs, up to what its output can carry (BR-A5).</summary>
/// <param name="Priorities">
/// A priority merger's priority per input, counted from 0: a higher number is served first, equal numbers share
/// evenly. Null or empty for a plain merger.
/// </param>
public sealed record FlowMerger(BuildableId Id, Carries Carries = Carries.Anything, IReadOnlyList<int>? Priorities = null) : FlowNode(Id);