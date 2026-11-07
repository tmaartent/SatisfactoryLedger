using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.World.Domain.Tests;

/// <summary>What a drone burns (BR-A16): the energy of a round trip, paid in fuel, as often as its latest trip took.</summary>
public class DroneRouteTests
{
    // The Docs file's drone port: 24,000 MJ a trip and 6 MJ per metre flown; packaged rocket fuel has 7,200 MJ.
    private static readonly DroneTripCost DronePort = new(24_000, 6);
    private const decimal RocketFuel = 7_200;

    [Fact]
    public void A_round_trip_costs_its_fixed_energy_plus_the_energy_of_every_metre_there_and_back()
    {
        var route = new DroneRoute(DistanceMetres: 3_000, LatestTripSeconds: 300);

        Assert.Equal(60_000m, route.TripEnergy(DronePort));
    }

    [Fact]
    public void A_drone_burns_a_trips_worth_of_fuel_as_often_as_its_latest_trip_took()
    {
        // 60,000 MJ is 8⅓ rocket fuel a trip; a trip every 5 minutes is 0.2 a minute.
        var route = new DroneRoute(DistanceMetres: 3_000, LatestTripSeconds: 300);

        Assert.Equal(Rate.Of(5, 3), route.FuelPerMinute(DronePort, RocketFuel));
    }

    [Fact]
    public void A_drone_whose_latest_trip_took_long_burns_little()
    {
        var route = new DroneRoute(DistanceMetres: 3_000, LatestTripSeconds: 30_000);

        Assert.Equal(Rate.Of(1, 60), route.FuelPerMinute(DronePort, RocketFuel));
    }

    [Fact]
    public void A_drone_between_two_ports_side_by_side_still_pays_the_fixed_cost()
    {
        var route = new DroneRoute(DistanceMetres: 0, LatestTripSeconds: 204);

        Assert.Equal(24_000m, route.TripEnergy(DronePort));
    }

    [Fact]
    public void A_trip_takes_time()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DroneRoute(DistanceMetres: 3_000, LatestTripSeconds: 0));
    }

    [Fact]
    public void A_fuel_has_energy()
    {
        var route = new DroneRoute(DistanceMetres: 3_000, LatestTripSeconds: 300);

        Assert.Throws<ArgumentOutOfRangeException>(() => route.FuelPerMinute(DronePort, 0));
    }
}
