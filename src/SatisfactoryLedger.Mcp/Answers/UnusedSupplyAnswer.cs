namespace SatisfactoryLedger.Mcp.Answers;

/// <summary>A miner, extractor or station that could deliver more than is taken from it.</summary>
internal sealed record UnusedSupplyAnswer(
    string Id, string Building, string Item, double OfferedPerMinute, double DeliveredPerMinute, double UnusedPerMinute, Metres? PositionMetres);
