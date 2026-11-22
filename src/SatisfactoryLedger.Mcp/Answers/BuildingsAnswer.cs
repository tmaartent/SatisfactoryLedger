namespace SatisfactoryLedger.Mcp.Answers;

/// <summary>
/// Buildings of one kind where a shortage or blockage starts, or that take or send the item besides: how many, how much
/// per minute (for those others), the first ids, and where the first stands.
/// </summary>
internal sealed record BuildingsAnswer(string Building, string? Recipe, int Count, double? PerMinute, IReadOnlyList<string> Ids, Metres? PositionMetres);
