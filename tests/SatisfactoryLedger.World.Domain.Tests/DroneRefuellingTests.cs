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

    private static IReadOnlyDictionary<BuildableId, Rate> BurnRates(IEnumerable<Drone> drones, params BuildableId[] fuelled) =>
        DroneRefuelling.BurnRates(drones, fuelled.ToDictionary(port => port, _ => RocketFuel), Cost, EnergyOf);

    [Fact]
    public void A_drone_refuels_at_its_home_port_when_fuel_is_in_its_slot()
    {
        var rates = BurnRates([new Drone(Home, Hub, Route)], Home);

        Assert.Equal(new Dictionary<BuildableId, Rate> { [Home] = RoutePerMinute }, rates);
    }

    [Fact]
    public void A_drone_whose_home_port_has_no_fuel_refuels_at_the_port_it_is_paired_with()
    {
        var rates = BurnRates([new Drone(Home, Hub, Route)], Hub);

        Assert.Equal(new Dictionary<BuildableId, Rate> { [Hub] = RoutePerMinute }, rates);
    }

    [Fact]
    public void A_drone_with_fuel_at_both_ports_refuels_at_home()
    {
        var rates = BurnRates([new Drone(Home, Hub, Route)], Home, Hub);

        Assert.Equal(new Dictionary<BuildableId, Rate> { [Home] = RoutePerMinute }, rates);
    }

    [Fact]
    public void A_port_where_several_drones_refuel_burns_for_all_of_them()
    {
        var rates = BurnRates([new Drone(Home, Hub, Route), new Drone(OtherHome, Hub, Route)], Hub);

        Assert.Equal(RoutePerMinute * 2, rates[Hub]);
    }

    [Fact]
    public void A_drone_with_no_fuel_at_either_port_burns_nothing_the_save_can_tell()
    {
        Assert.Empty(BurnRates([new Drone(Home, Hub, Route)]));
    }

    [Fact]
    public void A_port_where_a_drone_without_a_recorded_trip_refuels_burns_an_unknown_rate()
    {
        var rates = BurnRates([new Drone(Home, Hub, Route), new Drone(OtherHome, Hub, Route: null)], Hub);

        Assert.Empty(rates);
    }

    [Fact]
    public void A_port_whose_fuel_the_catalog_has_no_energy_for_burns_an_unknown_rate()
    {
        var rates = DroneRefuelling.BurnRates([new Drone(Home, Hub, Route)],
            new Dictionary<BuildableId, ItemId> { [Home] = ItemId.Of("Desc_IronPlate_C") }, Cost, EnergyOf);

        Assert.Empty(rates);
    }
}
