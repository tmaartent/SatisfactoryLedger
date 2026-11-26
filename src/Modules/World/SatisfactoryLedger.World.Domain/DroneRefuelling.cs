using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.World.Domain;

/// <summary>Where drones refuel, and what each port burns for them (BR-A16).</summary>
public static class DroneRefuelling
{
    /// <summary>
    /// Per drone port with fuel in its slot, what it burns. A drone refuels at its home port when fuel is in its slot,
    /// otherwise at its paired port when fuel is in that one's; a drone with fuel at neither is left out. A port where
    /// drones refuel burns for all of them, <see cref="FuelBurn.NotKnown"/> when one of them hasn't made a trip yet or
    /// the fuel's energy isn't known. A port where no drone refuels burns nothing (decided 2026-11-26, issue #44).
    /// </summary>
    /// <param name="fuelInSlot">Per drone port with fuel in its slot, that fuel.</param>
    /// <param name="energyOf">The energy in one item of a fuel, in megajoules; null when it isn't known.</param>
    public static IReadOnlyDictionary<BuildableId, FuelBurn> FuelBurns(
        IEnumerable<Drone> drones,
        IReadOnlyDictionary<BuildableId, ItemId> fuelInSlot,
        DroneTripCost cost,
        Func<ItemId, decimal?> energyOf)
    {
        ArgumentNullException.ThrowIfNull(drones);
        ArgumentNullException.ThrowIfNull(fuelInSlot);

        Dictionary<BuildableId, FuelBurn> burns = fuelInSlot.Keys.ToDictionary(port => port, FuelBurn (_) => new FuelBurn.NoDroneRefuels());
        foreach (Drone drone in drones)
        {
            BuildableId? port = fuelInSlot.ContainsKey(drone.Home) ? drone.Home
                : fuelInSlot.ContainsKey(drone.Paired) ? drone.Paired
                : null;
            if (port is not { } refuelsAt)
            {
                continue;
            }

            Rate? burnt = drone.Route is { } route && energyOf(fuelInSlot[refuelsAt]) is { } energy
                ? route.FuelPerMinute(cost, energy)
                : null;
            // Once one drone's part is unknown, so is the port's.
            burns[refuelsAt] = (burns[refuelsAt], burnt) switch
            {
                (FuelBurn.NoDroneRefuels, { } rate) => new FuelBurn.Known(rate),
                (FuelBurn.Known sofar, { } rate) => new FuelBurn.Known(sofar.PerMinute + rate),
                _ => new FuelBurn.NotKnown(),
            };
        }

        return burns;
    }
}
