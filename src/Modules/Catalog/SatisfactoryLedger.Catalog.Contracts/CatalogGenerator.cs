namespace SatisfactoryLedger.Catalog.Contracts;

/// <summary>
/// A generator: it burns a fuel at <see cref="PowerMegawatts"/> ÷ the fuel's energy per second, and needs its
/// supplemental fluid at <see cref="PowerMegawatts"/> × <see cref="SupplementalLitresPerMegajoule"/> litres per second.
/// </summary>
/// <param name="Id">The building's class name, e.g. <c>Build_GeneratorNuclear_C</c>.</param>
public sealed record CatalogGenerator(string Id, decimal PowerMegawatts, decimal SupplementalLitresPerMegajoule, IReadOnlyList<CatalogGeneratorFuel> Fuels);