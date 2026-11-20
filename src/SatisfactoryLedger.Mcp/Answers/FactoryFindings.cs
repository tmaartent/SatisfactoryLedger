using SatisfactoryLedger.Mcp.LedgerApi;

namespace SatisfactoryLedger.Mcp.Answers;

/// <summary>One factory's findings: what it takes in and makes, its power, and where it loses output.</summary>
internal sealed record FactoryFindings(
    string FactoryId,
    string Name,
    MachineCounts Machines,
    IReadOnlyList<ItemFlowAnswer> Input,
    IReadOnlyList<ItemFlowAnswer> Output,
    FactoryPower? Power,
    IReadOnlyList<MachineGroup> BelowFullSpeed);
