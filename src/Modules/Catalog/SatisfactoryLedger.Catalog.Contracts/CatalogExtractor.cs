namespace SatisfactoryLedger.Catalog.Contracts;

/// <param name="Id">The building's class name, e.g. <c>Build_MinerMk1_C</c>.</param>
/// <param name="AmountPerCycle">Items, or cubic metres for a fluid, on a normal-purity node at 100% clock speed.</param>
public sealed record CatalogExtractor(string Id, decimal AmountPerCycle, decimal CycleSeconds);