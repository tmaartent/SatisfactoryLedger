namespace SatisfactoryLedger.Mcp.Answers;

/// <summary>
/// What a bottleneck needs to carry all that's offered to it (decided 2026-11-22): <see cref="Lines"/> side by side of
/// <see cref="Tier"/>. The lowest tier that carries it alone, or lines of the highest when none does.
/// </summary>
internal sealed record MoreCapacityAnswer(string Tier, int Lines, double CarriesPerMinute);