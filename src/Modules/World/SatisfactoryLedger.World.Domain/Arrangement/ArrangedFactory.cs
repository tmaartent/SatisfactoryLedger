namespace SatisfactoryLedger.World.Domain.Arrangement;

/// <summary>A factory to place: its space as built, moved but never turned (decided 2026-11-12).</summary>
/// <param name="Now">Where it stands now.</param>
public sealed record ArrangedFactory(string Id, GridRectangle Now);
