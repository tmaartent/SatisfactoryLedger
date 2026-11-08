namespace SatisfactoryLedger.Catalog.Contracts;

/// <param name="EnergyMegajoules">Per item, or per cubic metre of a fluid fuel.</param>
/// <param name="Supplemental">The fluid it needs besides the fuel (water); null when none.</param>
/// <param name="Byproduct">What burning one item leaves (uranium waste), <paramref name="ByproductPerFuel"/> of it; null when nothing.</param>
public sealed record CatalogGeneratorFuel(string Fuel, decimal EnergyMegajoules, string? Supplemental, string? Byproduct, decimal ByproductPerFuel);