namespace SatisfactoryLedger.World.Analysis;

/// <summary>
/// How the generators of a snapshot or factory run: at 100%, or below it and why (decided 2026-11-11, BR-A15). A
/// generator whose fuel isn't known isn't simulated: it counts among the generators and as fuel unknown only.
/// </summary>
internal sealed record GeneratorSummary(int Generators, int Running, int Starved, int OutputBlocked, int FuelUnknown);
