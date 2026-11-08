using SatisfactoryLedger.Catalog.Contracts;

namespace SatisfactoryLedger.World.Snapshots;

/// <summary>The ground a building class covers, seen from above, in its own coordinates (centimetres), as the Catalog said at import.</summary>
/// <summary>A generator as the Catalog said at import (BR-A8): power, litres of water per MJ, and the fuels it burns.</summary>
internal sealed record GeneratorDocument(string Id, decimal PowerMegawatts, decimal SupplementalLitresPerMegajoule, IReadOnlyList<GeneratorFuelDocument> Fuels)
{
    public static GeneratorDocument From(CatalogGenerator generator) => new(
        generator.Id, generator.PowerMegawatts, generator.SupplementalLitresPerMegajoule,
        [.. generator.Fuels.Select(fuel => new GeneratorFuelDocument(fuel.Fuel, fuel.EnergyMegajoules, fuel.Supplemental, fuel.Byproduct, fuel.ByproductPerFuel))]);
}