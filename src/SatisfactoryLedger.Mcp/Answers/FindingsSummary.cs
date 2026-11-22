namespace SatisfactoryLedger.Mcp.Answers;

/// <summary>A snapshot's findings in brief: the counts, and where the most output is lost.</summary>
internal sealed record FindingsSummary(
    string SnapshotId,
    string World,
    DateTimeOffset SavedAt,
    MachineCounts Machines,
    int Bottlenecks,
    int UnusedSupply,
    int UnknownSupply,
    IReadOnlyList<MachineGroup> BelowFullSpeed);
