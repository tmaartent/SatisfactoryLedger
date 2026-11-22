namespace SatisfactoryLedger.Mcp.Answers;

/// <summary>One machine: what it makes, how fast it runs, and what holds it back (shares in percent).</summary>
internal sealed record MachineAnswer(
    string Id,
    string Building,
    string? Recipe,
    string Status,
    double UtilisationPercent,
    IReadOnlyDictionary<string, int>? ShortOf,
    IReadOnlyDictionary<string, int>? BlockedOn,
    string? OnStock,
    double? StockMinutes,
    Metres? PositionMetres);
