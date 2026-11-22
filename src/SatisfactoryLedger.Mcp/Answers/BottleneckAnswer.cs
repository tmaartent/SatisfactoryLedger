namespace SatisfactoryLedger.Mcp.Answers;

/// <summary>A belt or pipe at its capacity while more is offered to it (BR-A12).</summary>
internal sealed record BottleneckAnswer(
    string Id, string Building, double CarriesPerMinute, double CapacityPerMinute, double OfferedMorePerMinute, Metres? PositionMetres);
