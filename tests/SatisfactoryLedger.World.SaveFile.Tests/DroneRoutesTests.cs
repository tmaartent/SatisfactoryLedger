using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.SaveFile.Tests;

/// <summary>
/// A drone's route from what the save says (BR-A16): where its home port and the paired port stand, and how long its
/// latest trips took. No fixture has a drone flying between two ports yet, so these use the save's values directly.
/// </summary>
public class DroneRoutesTests
{
    private static readonly BuildableId Paired = BuildableId.Of("Persistent_Level:PersistentLevel.Build_DroneStation_C_2");

    [Fact]
    public void The_distance_between_two_ports_is_measured_flat_in_metres()
    {
        // 3,000 m east and 4,000 m north, and 200 m higher: the game leaves the height out.
        SavedDroneRoute? route = DroneRoutes.Of(new SavedPosition(0, 0, 0), (Paired, new SavedPosition(300_000, 400_000, 20_000)), [300f]);

        Assert.Equal((Paired, 5_000m), (route!.Paired, route.DistanceMetres));
    }

    [Fact]
    public void The_latest_trip_is_the_one_the_save_records()
    {
        SavedDroneRoute? route = DroneRoutes.Of(new SavedPosition(0, 0, 0), (Paired, new SavedPosition(100, 0, 0)), [320.83008f]);

        Assert.Equal(320.83m, route!.LatestTripSeconds);
    }

    [Fact]
    public void More_than_one_recorded_trip_is_averaged()
    {
        SavedDroneRoute? route = DroneRoutes.Of(new SavedPosition(0, 0, 0), (Paired, new SavedPosition(100, 0, 0)), [300f, 400f]);

        Assert.Equal(350m, route!.LatestTripSeconds);
    }

    [Fact]
    public void A_port_without_a_paired_port_has_no_route()
    {
        Assert.Null(DroneRoutes.Of(new SavedPosition(0, 0, 0), paired: null, [300f]));
    }

    [Fact]
    public void A_drone_without_a_recorded_trip_has_a_route_but_no_trip_time()
    {
        SavedDroneRoute? route = DroneRoutes.Of(new SavedPosition(0, 0, 0), (Paired, new SavedPosition(100, 0, 0)), []);

        Assert.Equal((Paired, (decimal?)null), (route!.Paired, route.LatestTripSeconds));
    }
}
