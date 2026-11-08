namespace SatisfactoryLedger.World.Domain.Flow;

/// <summary>A belt, lift or pipe at its capacity while more is offered upstream of it (BR-A12).</summary>
public sealed record Bottleneck(BuildableId Conduit, double Capacity, double Flow, double OfferedMore);