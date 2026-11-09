namespace SatisfactoryLedger.World.Contracts.Analysis;

public sealed record UnsettledPartDocument(IReadOnlyList<string> Buildables, int Sweeps);