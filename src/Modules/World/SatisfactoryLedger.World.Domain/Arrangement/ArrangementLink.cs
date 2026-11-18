namespace SatisfactoryLedger.World.Domain.Arrangement;

/// <summary>An input of a factory: what feeds it, from another factory or a fixed place.</summary>
/// <param name="Belts">The belts (or pipes) it needs: its rate over a top-tier belt's capacity, rounded up (decided 2026-11-12).</param>
public sealed record ArrangementLink(string From, string To, int Belts);
