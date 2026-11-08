namespace SatisfactoryLedger.Catalog.Contracts;

/// <param name="EnergyMegajoules">Per item.</param>
public sealed record CatalogDroneFuel(string Fuel, decimal EnergyMegajoules);