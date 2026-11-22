namespace SatisfactoryLedger.Mcp.Answers;

/// <summary>
/// Machines of one building and recipe held back for the same reason. <see cref="MachinesLost"/> is the output lost, in
/// machines' worth: two at half speed lose one. <see cref="ShortOf"/> and <see cref="BlockedOn"/> give each item's
/// share in percent, the worst of the group.
/// </summary>
internal sealed record MachineGroup(
    string Building,
    string? Recipe,
    string Status,
    int Machines,
    double AverageUtilisationPercent,
    double MachinesLost,
    IReadOnlyDictionary<string, int>? ShortOf,
    IReadOnlyDictionary<string, int>? BlockedOn);
