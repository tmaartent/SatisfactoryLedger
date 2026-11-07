using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.World.Domain;

/// <summary>
/// The route of a drone between its home port and the port it's paired with (BR-A16): what a round trip costs, and how
/// much fuel that is per minute.
/// </summary>
/// <param name="DistanceMetres">Between the two ports, measured flat: the game leaves height out.</param>
/// <param name="LatestTripSeconds">
/// How long its latest round trip took, as the save records it. The game's port screen bases its fuel figure on it, so a
/// drone that sat docked waiting shows a low rate.
/// </param>
public sealed record DroneRoute(decimal DistanceMetres, decimal LatestTripSeconds)
{
    public decimal DistanceMetres { get; } = DistanceMetres >= 0 ? DistanceMetres : throw new ArgumentOutOfRangeException(nameof(DistanceMetres), DistanceMetres, "A distance isn't negative");

    public decimal LatestTripSeconds { get; } = LatestTripSeconds > 0 ? LatestTripSeconds : throw new ArgumentOutOfRangeException(nameof(LatestTripSeconds), LatestTripSeconds, "A trip takes time");

    /// <summary>The energy a round trip costs: the fixed cost, plus the cost of every metre there and back.</summary>
    public decimal TripEnergy(DroneTripCost cost)
    {
        ArgumentNullException.ThrowIfNull(cost);
        return cost.FixedMegajoules + cost.PerMetreMegajoules * 2 * DistanceMetres;
    }

    /// <summary>A trip's energy in items of a fuel with <paramref name="fuelEnergyMegajoules"/> each, once every latest trip.</summary>
    public Rate FuelPerMinute(DroneTripCost cost, decimal fuelEnergyMegajoules)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fuelEnergyMegajoules);
        return Rate.FromRecipe(TripEnergy(cost), LatestTripSeconds * fuelEnergyMegajoules);
    }
}

/// <summary>What a drone's round trip costs, from the Docs file: a fixed part, and a part per metre flown.</summary>
public sealed record DroneTripCost(decimal FixedMegajoules, decimal PerMetreMegajoules);
