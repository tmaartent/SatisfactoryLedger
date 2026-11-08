namespace SatisfactoryLedger.World.Analysis;

/// <summary>A connected part of the world that didn't settle: its findings are approximate (BR-A10).</summary>
/// <param name="Example">One of its buildables, to find the part in the game.</param>
internal sealed record ApproximatePart(int Buildables, int Sweeps, string Example);