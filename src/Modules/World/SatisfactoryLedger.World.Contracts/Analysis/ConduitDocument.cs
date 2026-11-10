namespace SatisfactoryLedger.World.Contracts.Analysis;

/// <param name="Capacity">Per minute; null when there's no limit.</param>
public sealed record ConduitDocument(string Id, double? Capacity, Carries Carries) : FlowNodeDocument(Id);