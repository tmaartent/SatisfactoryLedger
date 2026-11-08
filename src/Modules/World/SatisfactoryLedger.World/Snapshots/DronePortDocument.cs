using SatisfactoryLedger.Catalog.Contracts;

namespace SatisfactoryLedger.World.Snapshots;

/// <summary>The drone port as the Catalog said at import (BR-A16): what a trip costs, and the energy of the fuels its ports hold.</summary>
/// <param name="Fuels">Per fuel, megajoules per item.</param>
internal sealed record DronePortDocument(
    string Id, decimal TripEnergyMegajoules, decimal TripEnergyPerMetreMegajoules, IReadOnlyDictionary<string, decimal> Fuels)
{
    /// <param name="fuelsHeld">The fuels in the save's fuel slots: only theirs are kept.</param>
    public static DronePortDocument From(CatalogDronePort port, IReadOnlySet<string> fuelsHeld) => new(
        port.Id, port.TripEnergyMegajoules, port.TripEnergyPerMetreMegajoules,
        port.Fuels.Where(fuel => fuelsHeld.Contains(fuel.Fuel)).OrderBy(fuel => fuel.Fuel, StringComparer.Ordinal)
            .ToDictionary(fuel => fuel.Fuel, fuel => fuel.EnergyMegajoules, StringComparer.Ordinal));
}