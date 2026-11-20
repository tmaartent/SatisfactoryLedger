namespace SatisfactoryLedger.Mcp.Answers;

/// <summary>
/// One step on the way to where it starts: machines of one kind short of an item (upstream) or that can't get it away
/// (downstream), and by how much per minute for the whole step.
/// </summary>
internal sealed record TraceStepAnswer(string Building, string? Recipe, int Machines, string? ShortOf, string? CannotLeave, double PerMinute);
