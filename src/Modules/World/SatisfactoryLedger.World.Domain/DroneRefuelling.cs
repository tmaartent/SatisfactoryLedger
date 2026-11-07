using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.World.Domain;

/// <summary>A drone, between its home port and the port it's paired with (BR-A16).</summary>
/// <param name="Route">Null when it hasn't made a trip yet: what it burns isn't known.</param>
public sealed record Drone(BuildableId Home, BuildableId Paired, DroneRoute? Route);

/// <summary>Where drones refuel, and what each port burns for them (BR-A16).</summary>
public static class DroneRefuelling
{
    /// <summary>
    /// Per port, the fuel per minute the drones refuelling there burn. A drone refuels at its home port when fuel is in
    /// its slot, otherwise at its paired port when fuel is in that one's; a drone with fuel at neither is left out. A port
    /// is left out too when what it burns isn't known: a drone refuelling there hasn't made a trip yet, or the fuel's
    /// energy isn't known.
    /// </summary>
    /// <param name="fuelInSlot">Per drone port with fuel in its slot, that fuel.</param>
    /// <param name="energyOf">The energy in one item of a fuel, in megajoules; null when it isn't known.</param>
    public static IReadOnlyDictionary<BuildableId, Rate> BurnRates(
        IEnumerable<Drone> drones,
        IReadOnlyDictionary<BuildableId, ItemId> fuelInSlot,
        DroneTripCost cost,
        Func<ItemId, decimal?> energyOf)
    {
        ArgumentNullException.ThrowIfNull(drones);
        ArgumentNullException.ThrowIfNull(fuelInSlot);

        Dictionary<BuildableId, Rate?> burns = [];
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
            burns[refuelsAt] = burns.TryGetValue(refuelsAt, out Rate? sofar)
                ? sofar is not null && burnt is not null ? sofar + burnt : null
                : burnt;
        }

        return burns.Where(entry => entry.Value is not null).ToDictionary(entry => entry.Key, entry => entry.Value!);
    }
}
