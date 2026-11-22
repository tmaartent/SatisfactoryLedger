namespace SatisfactoryLedger.Mcp.Answers;

/// <summary>
/// Where a shortage or blockage starts: why (as the API's code, and as the ledger's term from <see cref="TraceCauses"/>),
/// of which item, at which buildings, and the steps from the machines traced. <see cref="PerMinute"/> is what those
/// buildings make, offer, carry or take of the item.
/// </summary>
internal sealed record TraceStartAnswer(
    string Cause,
    string CauseTerm,
    string Item,
    IReadOnlyList<BuildingsAnswer> At,
    IReadOnlyList<TraceStepAnswer> Path,
    double PerMinute,
    IReadOnlyList<BuildingsAnswer>? Others,
    IReadOnlyDictionary<string, int>? ShortOf);
