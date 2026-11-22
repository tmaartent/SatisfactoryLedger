using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Tracing;

/// <summary>
/// Buildables of one class and recipe in a trace, together: how many, the first ids, where the first stands, and (for
/// others that take or send the item) how much per minute.
/// </summary>
internal sealed record TraceGroupResponse(string ClassName, string? Recipe, int Count, double PerMinute, IReadOnlyList<string> Ids, PositionDocument? Position);
