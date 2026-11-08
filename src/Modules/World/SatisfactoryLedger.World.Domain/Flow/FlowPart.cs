namespace SatisfactoryLedger.World.Domain.Flow;

/// <summary>
/// A connected part of the world, simulated on its own. When it didn't settle within the limit of sweeps, its numbers
/// are approximate: it came close, but some flows still moved by more than 0.0001 a minute (BR-A10).
/// </summary>
public sealed record FlowPart(IReadOnlyList<BuildableId> Buildables, bool Settled, int Sweeps);