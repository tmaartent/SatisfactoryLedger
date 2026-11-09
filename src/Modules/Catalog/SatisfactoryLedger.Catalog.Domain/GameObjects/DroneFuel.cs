namespace SatisfactoryLedger.Catalog.Domain.GameObjects;

/// <summary>A fuel a drone can burn, and the energy in one item of it.</summary>
public sealed record DroneFuel(ItemId Fuel, decimal EnergyMegajoules)
{
    public decimal EnergyMegajoules { get; } = EnergyMegajoules > 0 ? EnergyMegajoules : throw new ArgumentOutOfRangeException(nameof(EnergyMegajoules), EnergyMegajoules, "A fuel has energy");
}