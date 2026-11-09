namespace SatisfactoryLedger.World.Contracts.Analysis;

/// <param name="BurnRate">For a drone port that loads: its fuel per minute; null when that isn't known (BR-A16).</param>
public sealed record SinkDocument(string Id, IReadOnlyList<string>? Burns = null, bool LeftoversOnly = false, double? BurnRate = null) : FlowNodeDocument(Id);