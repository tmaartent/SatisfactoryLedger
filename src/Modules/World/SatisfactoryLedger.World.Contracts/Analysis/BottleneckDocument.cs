namespace SatisfactoryLedger.World.Contracts.Analysis;

public sealed record BottleneckDocument(string Id, double Capacity, double Flow, double OfferedMore);