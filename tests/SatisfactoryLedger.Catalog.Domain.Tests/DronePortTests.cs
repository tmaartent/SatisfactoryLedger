namespace SatisfactoryLedger.Catalog.Domain.Tests;

/// <summary>The drone port from the Docs file (BR-A16): what a trip costs, and the fuels a drone can burn.</summary>
public class DronePortTests
{
    private static readonly ItemId RocketFuel = ItemId.Of("Desc_PackagedRocketFuel_C");
    private static readonly ItemId Battery = ItemId.Of("Desc_Battery_C");

    // As the Docs file has it: 24,000 MJ a trip and 6 MJ per metre; packaged rocket fuel 7,200 MJ.
    private static DronePort DronePort() =>
        Domain.DronePort.Create(DronePortId.Of("Build_DroneStation_C"), "Drone Port", 24_000, 6, [new DroneFuel(RocketFuel, 7_200)]);

    [Fact]
    public void A_drone_port_knows_the_energy_of_each_fuel_a_drone_can_burn()
    {
        Assert.Equal(7_200, DronePort().EnergyOf(RocketFuel));
    }

    [Fact]
    public void A_drone_port_does_not_know_the_energy_of_what_is_no_fuel()
    {
        Assert.Null(DronePort().EnergyOf(ItemId.Of("Desc_IronPlate_C")));
    }

    [Fact]
    public void A_drone_fuel_has_energy()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DroneFuel(RocketFuel, 0));
    }

    [Fact]
    public void A_trip_costs_energy()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Domain.DronePort.Create(DronePortId.Of("Build_DroneStation_C"), "Drone Port", 0, 6, []));
    }

    [Fact]
    public void A_drone_port_from_a_newer_game_file_takes_its_trip_cost_and_fuels()
    {
        DronePort stored = DronePort();
        stored.MarkNoLongerInGame();

        stored.UpdateFromGame(Domain.DronePort.Create(DronePortId.Of("Build_DroneStation_C"), "Drone Port", 20_000, 5,
            [new DroneFuel(Battery, 6_000)]));

        Assert.Equal((20_000m, 5m, 6_000m, (decimal?)null, true),
            (stored.TripEnergyMegajoules, stored.TripEnergyPerMetreMegajoules, stored.EnergyOf(Battery), stored.EnergyOf(RocketFuel), stored.IsInGame));
    }
}
