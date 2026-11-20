namespace SatisfactoryLedger.Mcp.LedgerApi;

/// <summary>
/// One machine's finding: how fast it runs and why (BR-A11, BR-A17). <see cref="StarvedOf"/> and <see cref="BlockedOn"/>
/// give each item's share, from 0 to 1.
/// </summary>
internal sealed record MachineFinding(
    string Id,
    string ClassName,
    string? Recipe,
    double Utilisation,
    string Limit,
    PositionDocument? Position,
    IReadOnlyDictionary<string, double>? StarvedOf,
    IReadOnlyDictionary<string, double>? BlockedOn,
    string? OnStock,
    double? StockMinutes);
