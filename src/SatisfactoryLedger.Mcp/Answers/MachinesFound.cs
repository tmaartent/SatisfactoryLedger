namespace SatisfactoryLedger.Mcp.Answers;

/// <summary>The machines found: how many in all, and the slowest of them first.</summary>
internal sealed record MachinesFound(int Total, IReadOnlyList<MachineAnswer> Machines);
