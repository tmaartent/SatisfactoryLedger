namespace SatisfactoryLedger.World.Simulation;

public sealed record UnsettledPartDocument(IReadOnlyList<string> Buildables, int Sweeps);