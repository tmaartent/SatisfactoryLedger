namespace SatisfactoryLedger.World.Domain.Tracing;

/// <summary>
/// One step of a trace: machines short of an item (upstream) or that can't get an item away (downstream), and by how
/// much per minute, all together.
/// </summary>
public sealed record TraceStep(IReadOnlyList<BuildableId> Machines, ItemId Item, double PerMinute);
