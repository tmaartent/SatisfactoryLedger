using SatisfactoryLedger.Catalog.Contracts;
using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Analysis;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.SaveFile;
using SatisfactoryLedger.World.Contracts.Analysis;
using SatisfactoryLedger.World.Snapshots;
using SatisfactoryLedger.World.Simulation;

namespace SatisfactoryLedger.World.Tests;

/// <summary>
/// Drone port findings (BR-A16): what each port where drones refuel burns, and what fuel reaches it. A small world: a
/// container feeds rocket fuel to a hub; one drone refuels there, another at its own home port, which nothing feeds.
/// </summary>
public class DronePortFindingsTests
{
    private const string RocketFuel = "Desc_PackagedRocketFuel_C";
    private static readonly string Hub = TestWorldSnapshot.Id("Build_DroneStation_C_1");
    private static readonly string Home = TestWorldSnapshot.Id("Build_DroneStation_C_2");
    private static readonly string Lonely = TestWorldSnapshot.Id("Build_DroneStation_C_3");
    private static readonly string Container = TestWorldSnapshot.Id("Build_StorageContainerMk1_C_1");
    private static readonly string Belt = TestWorldSnapshot.Id("Build_ConveyorBeltMk1_C_1");

    private static readonly AnalysisResponse Analysis = Analyse();

    private static AnalysisResponse Analyse()
    {
        static HashSet<string> Fuel() => [RocketFuel];
        // Each drone flies 3 km and its latest trip took 5 minutes: 5/3 rocket fuel a minute.
        var saved = new SavedWorld(
            new SaveInfo("Drones", 517554, new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc)),
            [
                new SavedLogistics(BuildableId.Of(Container), "Build_StorageContainerMk1_C", LogisticsKind.StorageContainer)
                {
                    Stock = new Dictionary<string, decimal> { [RocketFuel] = 500 },
                },
                new SavedLogistics(BuildableId.Of(Belt), "Build_ConveyorBeltMk1_C", LogisticsKind.Belt),
                new SavedProvider(BuildableId.Of(Hub), "Build_DroneStation_C", Node: null, ClockSpeed.Percent(100)) { Burns = Fuel() },
                new SavedConsumer(BuildableId.Of(Home), "Build_DroneStation_C")
                {
                    Burns = new HashSet<string>(),
                    Drone = new SavedDroneRoute(BuildableId.Of(Hub), 3_000, 300),
                },
                new SavedConsumer(BuildableId.Of(Lonely), "Build_DroneStation_C")
                {
                    Burns = Fuel(),
                    Drone = new SavedDroneRoute(BuildableId.Of(Hub), 3_000, 300),
                },
            ],
            [new Connection(BuildableId.Of(Container), BuildableId.Of(Belt)), new Connection(BuildableId.Of(Belt), BuildableId.Of(Hub))],
            []);
        CatalogCopy catalog = TestWorldSnapshot.Catalog with
        {
            DronePorts = [new CatalogDronePort("Build_DroneStation_C", 24_000, 6, [new(RocketFuel, 7_200)])],
        };
        SnapshotDocument snapshot = SnapshotDocument.FromJson(SnapshotDocument.Create(saved, catalog).ToJson());
        return SnapshotAnalysis.For(snapshot, AnalysisDocument.FromJson(AnalysisDocument.Compute(snapshot.ToFlowNetwork()).ToJson()));
    }

    [Fact]
    public void Every_drone_port_where_drones_refuel_is_a_finding_with_its_fuel()
    {
        Assert.Equal(
            new Dictionary<string, string> { [Hub] = RocketFuel, [Lonely] = RocketFuel },
            Analysis.DronePorts.ToDictionary(port => port.Id, port => port.Fuel));
    }

    [Fact]
    public void A_drone_port_fed_by_a_belt_gets_all_the_fuel_its_drones_burn()
    {
        DronePortFinding hub = Assert.Single(Analysis.DronePorts, port => port.Id == Hub);

        Assert.Equal((1.6667, 1.6667), (hub.Burns, hub.Supplied));
    }

    [Fact]
    public void A_drone_port_nothing_feeds_gets_no_fuel()
    {
        DronePortFinding lonely = Assert.Single(Analysis.DronePorts, port => port.Id == Lonely);

        Assert.Equal((1.6667, 0.0), (lonely.Burns, lonely.Supplied));
    }

    [Fact]
    public void Drone_ports_are_listed_worst_first()
    {
        Assert.Equal([Lonely, Hub], Analysis.DronePorts.Select(port => port.Id));
    }
}
