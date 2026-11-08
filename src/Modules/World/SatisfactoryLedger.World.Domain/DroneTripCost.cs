namespace SatisfactoryLedger.World.Domain;

/// <summary>What a drone's round trip costs, from the Docs file: a fixed part, and a part per metre flown.</summary>
public sealed record DroneTripCost(decimal FixedMegajoules, decimal PerMetreMegajoules);