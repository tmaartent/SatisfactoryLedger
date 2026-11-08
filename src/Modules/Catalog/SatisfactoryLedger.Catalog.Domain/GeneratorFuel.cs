namespace SatisfactoryLedger.Catalog.Domain;

/// <summary>A fuel a generator can burn, and what burning it takes and leaves.</summary>
/// <param name="EnergyMegajoules">The energy in one item, or in one cubic metre of a fluid fuel.</param>
/// <param name="Supplemental">The fluid it needs besides the fuel (water), or null.</param>
/// <param name="Byproduct">What it leaves per fuel item burned (uranium waste), or null.</param>
public sealed record GeneratorFuel(ItemId Fuel, decimal EnergyMegajoules, ItemId? Supplemental, ItemId? Byproduct, decimal ByproductPerFuel)
{
    public decimal EnergyMegajoules { get; } = EnergyMegajoules > 0 ? EnergyMegajoules : throw new ArgumentOutOfRangeException(nameof(EnergyMegajoules), EnergyMegajoules, "A fuel has energy");
}