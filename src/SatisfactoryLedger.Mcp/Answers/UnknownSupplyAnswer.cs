namespace SatisfactoryLedger.Mcp.Answers;

/// <summary>A station whose supply the save doesn't say, with what it was seen to deliver.</summary>
internal sealed record UnknownSupplyAnswer(string Id, string Building, IReadOnlyDictionary<string, double> DeliversPerMinute, Metres? PositionMetres);
