using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.World.Domain.Tests;

/// <summary>Where a drone refuels, and what that port burns for it (BR-A16).</summary>
public class DroneRefuellingTests
{
    private static readonly DroneTripCost Cost = new(24_000, 6);
    private static readonly ItemId RocketFuel = ItemId.Of("Desc_PackagedRocketFuel_C");
    private static readonly BuildableId Home = BuildableId.Of("home");
    private static readonly BuildableId Hub = BuildableId.Of("hub");
    private static readonly BuildableId OtherHome = BuildableId.Of("other home");

    // 3 km, a trip every 5 minutes: 60,000 MJ, so 8⅓ rocket fuel five times an hour, 5/3 a minute.
    private static readonly DroneRoute Route = new(3_000, 300);
    private static readonly Rate RoutePerMinute = Rate.Of(5, 3);

    private static decimal? EnergyOf(ItemId fuel) => fuel == RocketFuel ? 7_200 : null;

    private static IReadOnlyDictionary<BuildableId, FuelBurn> FuelBurns(IEnumerable<Drone> drones, params BuildableId[] fuelled) =>
        DroneRefuelling.FuelBurns(drones, fuelled.ToDictionary(port => port, _ => RocketFuel), Cost, EnergyOf);

    [Fact]
    public void A_drone_refuels_at_its_home_port_when_fuel_is_in_its_slot()
    {
        var burns = FuelBurns([new Drone(Home, Hub, Route)], Home);

        Assert.Equal(new FuelBurn.Known(RoutePerMinute), burns[Home]);
    }

    [Fact]
    public void A_drone_whose_home_port_has_no_fuel_refuels_at_the_port_it_is_paired_with()
    {
        var burns = FuelBurns([new Drone(Home, Hub, Route)], Hub);

        Assert.Equal(new Dictionary<BuildableId, FuelBurn> { [Hub] = new FuelBurn.Known(RoutePerMinute) }, burns);
    }

    [Fact]
    public void A_drone_with_fuel_at_both_ports_refuels_at_home()
    {
        var burns = FuelBurns([new Drone(Home, Hub, Route)], Home, Hub);

        Assert.Equal(new FuelBurn.Known(RoutePerMinute), burns[Home]);
    }

    [Fact]
    public void A_fuelled_port_where_every_drone_refuels_at_its_own_home_is_one_where_no_drone_refuels()
    {
        var burns = FuelBurns([new Drone(Home, Hub, Route), new Drone(OtherHome, Hub, Route)], Home, OtherHome, Hub);

        Assert.Equal(new FuelBurn.NoDroneRefuels(), burns[Hub]);
    }

    [Fact]
    public void A_fuelled_port_without_drones_is_one_where_no_drone_refuels()
    {
        var burns = FuelBurns([], Hub);

        Assert.Equal(new Dictionary<BuildableId, FuelBurn> { [Hub] = new FuelBurn.NoDroneRefuels() }, burns);
    }

    [Fact]
    public void A_port_where_several_drones_refuel_burns_for_all_of_them()
    {
        var burns = FuelBurns([new Drone(Home, Hub, Route), new Drone(OtherHome, Hub, Route)], Hub);

        Assert.Equal(new FuelBurn.Known(RoutePerMinute * 2), burns[Hub]);
    }

    [Fact]
    public void A_drone_with_no_fuel_at_either_port_burns_nothing_the_save_can_tell()
    {
        Assert.Empty(FuelBurns([new Drone(Home, Hub, Route)]));
    }

    [Fact]
    public void A_port_where_a_drone_without_a_recorded_trip_refuels_burns_an_unknown_rate()
    {
        var burns = FuelBurns([new Drone(Home, Hub, Route), new Drone(OtherHome, Hub, Route: null)], Hub);

        Assert.Equal(new FuelBurn.NotKnown(), burns[Hub]);
    }

    [Fact]
    public void A_port_whose_fuel_the_catalog_has_no_energy_for_burns_an_unknown_rate()
    {
        var burns = DroneRefuelling.FuelBurns([new Drone(Home, Hub, Route)],
            new Dictionary<BuildableId, ItemId> { [Home] = ItemId.Of("Desc_IronPlate_C") }, Cost, EnergyOf);

        Assert.Equal(new FuelBurn.NotKnown(), burns[Home]);
    }
}
