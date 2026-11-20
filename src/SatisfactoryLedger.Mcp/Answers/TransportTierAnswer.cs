namespace SatisfactoryLedger.Mcp.Answers;

/// <summary>A belt, lift or pipe tier and what it carries at most.</summary>
internal sealed record TransportTierAnswer(string Name, string Form, int CapacityPerMinute);
