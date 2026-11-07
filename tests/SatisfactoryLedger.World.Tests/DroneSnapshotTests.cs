using SatisfactoryLedger.Catalog.Contracts;
using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.SaveFile;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Tests;

/// <summary>
/// Drone ports in a stored snapshot (BR-A16): each drone's route, and what the port it refuels at burns. No fixture has a
/// drone flying between two ports, so these build a small world as the save reader gives it.
/// </summary>
public class DroneSnapshotTests
{
    private const string RocketFuel = "Desc_PackagedRocketFuel_C";
    private static readonly BuildableId Home = BuildableId.Of("Persistent_Level:PersistentLevel.Build_DroneStation_C_1");
    private static readonly BuildableId Hub = BuildableId.Of("Persistent_Level:PersistentLevel.Build_DroneStation_C_2");

    private static readonly CatalogCopy Catalog = TestWorldSnapshot.Catalog with
    {
        DronePorts = [new CatalogDronePort("Build_DroneStation_C", 24_000, 6, [new(RocketFuel, 7_200), new("Desc_Battery_C", 6_000)])],
    };

    /// <summary>A home port that sends cargo, its drone 3 km away from the hub, its latest trip 5 minutes.</summary>
    private static SnapshotDocument World(bool fuelAtHome, bool fuelAtHub, decimal? latestTripSeconds = 300)
    {
        var saved = new SavedWorld(
            new SaveInfo("Drones", 517554, new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc)),
            [
                new SavedConsumer(Home, "Build_DroneStation_C")
                {
                    Burns = fuelAtHome ? new HashSet<string> { RocketFuel } : new HashSet<string>(),
                    Drone = new SavedDroneRoute(Hub, 3_000, latestTripSeconds),
                },
                new SavedProvider(Hub, "Build_DroneStation_C", Node: null, ClockSpeed.Percent(100))
                {
                    Burns = fuelAtHub ? new HashSet<string> { RocketFuel } : new HashSet<string>(),
                    Stock = new Dictionary<string, decimal>(),
                },
            ],
            [],
            []);
        return SnapshotDocument.FromJson(SnapshotDocument.Create(saved, Catalog).ToJson());
    }

    private static Rate? BurnRateOf(SnapshotDocument snapshot, BuildableId id) =>
        snapshot.ToWorldGraph().Buildables.Single(buildable => buildable.Id == id) switch
        {
            Consumer consumer => consumer.BurnRate,
            Provider provider => provider.BurnRate,
            var other => throw new InvalidOperationException($"{other} is no drone port"),
        };

    [Fact]
    public void A_snapshot_copies_what_a_drone_trip_costs_and_the_energy_of_the_fuels_its_ports_hold()
    {
        DronePortDocument port = Assert.Single(World(fuelAtHome: true, fuelAtHub: false).DronePorts!);

        Assert.Equal(("Build_DroneStation_C", 24_000m, 6m), (port.Id, port.TripEnergyMegajoules, port.TripEnergyPerMetreMegajoules));
        Assert.Equal(new Dictionary<string, decimal> { [RocketFuel] = 7_200 }, port.Fuels);
    }

    [Fact]
    public void A_drone_port_with_fuel_in_its_slot_burns_what_its_drone_burns()
    {
        // 24,000 + 6 × 6,000 m = 60,000 MJ a trip, 8⅓ rocket fuel, every 5 minutes.
        Assert.Equal(Rate.Of(5, 3), BurnRateOf(World(fuelAtHome: true, fuelAtHub: false), Home));
    }

    [Fact]
    public void A_drone_whose_home_port_has_no_fuel_burns_at_the_hub_it_flies_to()
    {
        SnapshotDocument snapshot = World(fuelAtHome: false, fuelAtHub: true);

        Assert.Equal((Rate.Of(5, 3), (Rate?)null), (BurnRateOf(snapshot, Hub), BurnRateOf(snapshot, Home)));
    }

    [Fact]
    public void A_drone_that_has_made_no_trip_yet_burns_an_unknown_rate_like_before()
    {
        Assert.Null(BurnRateOf(World(fuelAtHome: true, fuelAtHub: false, latestTripSeconds: null), Home));
    }

    [Fact]
    public void A_snapshot_without_the_catalogs_drone_port_burns_an_unknown_rate_like_before()
    {
        var saved = SnapshotDocument.FromJson(World(fuelAtHome: true, fuelAtHub: false).ToJson()) with { DronePorts = null };

        Assert.Null(BurnRateOf(saved, Home));
    }

    [Fact]
    public void A_snapshot_keeps_each_drones_route()
    {
        BuildableDocument home = World(fuelAtHome: true, fuelAtHub: false).Buildables.Single(buildable => buildable.Id == Home.Id);

        Assert.Equal(new DroneDocument(Hub.Id, 3_000, 300), home.Drone);
    }
}
