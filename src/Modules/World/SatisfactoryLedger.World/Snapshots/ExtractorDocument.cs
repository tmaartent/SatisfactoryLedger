namespace SatisfactoryLedger.World.Snapshots;

/// <param name="Id">The building's class name, e.g. <c>Build_MinerMk1_C</c>.</param>
internal sealed record ExtractorDocument(string Id, decimal AmountPerCycle, decimal CycleSeconds);