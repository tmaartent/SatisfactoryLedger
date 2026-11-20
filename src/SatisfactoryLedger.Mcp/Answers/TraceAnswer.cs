namespace SatisfactoryLedger.Mcp.Answers;

/// <summary>A trace of one kind of machine: how many there are, how many were traced, where it starts, and a note when there's nothing to trace.</summary>
internal sealed record TraceAnswer(string Building, string? Recipe, int Machines, int Traced, IReadOnlyList<TraceStartAnswer> Starts, string? Note);
