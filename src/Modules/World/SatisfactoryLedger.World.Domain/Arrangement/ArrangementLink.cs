namespace SatisfactoryLedger.World.Domain.Arrangement;

/// <summary>An input of a factory: what feeds it, from another factory or a fixed place.</summary>
/// <param name="Belts">The belts it needs for solids: their rate over a top-tier belt's capacity, rounded up (decided 2026-11-12).</param>
/// <param name="WeightPercent">How much its belt work counts in placing, in percent: all of it between factories; the
/// setting for nodes, stations and drone ports (decided 2026-11-18).</param>
/// <param name="Pipes">The pipes it needs for fluids: their rate over a top-tier pipe's capacity, rounded up.</param>
public sealed record ArrangementLink(string From, string To, int Belts, int WeightPercent = 100, int Pipes = 0)
{
    /// <summary>What it lays on the belt floor: in placing, a pipe counts as a belt does.</summary>
    public int BeltsAndPipes => Belts + Pipes;
}
