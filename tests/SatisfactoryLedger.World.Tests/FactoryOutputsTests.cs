using SatisfactoryLedger.Catalog.Contracts;
using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Analysis;
using SatisfactoryLedger.World.Contracts.Analysis;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Factories;
using SatisfactoryLedger.World.SaveFile;
using SatisfactoryLedger.World.Snapshots;

using static SatisfactoryLedger.World.Tests.TestWorldSnapshot;

namespace SatisfactoryLedger.World.Tests;

/// <summary>
/// Where a factory's output goes, what its receivers would take, and what can't leave (BR-A18), in the test world.
/// Smelter A makes ingots for constructor C, which smelter B feeds too; in the model, A is output blocked at 12.5 of
/// its 30 a minute, and C, clocked at 75%, takes 22.5.
/// </summary>
public class FactoryOutputsTests
{
    private static StoredFactory Factory(SnapshotDocument snapshot, string name, params string[] machines) => new()
    {
        Id = Guid.CreateVersion7(),
        World = "Development",
        Name = name,
        Machines = [.. machines.Select(id => StoredMachine.From(snapshot.Buildables.Single(buildable => buildable.Id == id)))],
    };

    private static readonly StoredFactory Smelting = Factory(Document, "Smelting", SmelterA);
    private static readonly StoredFactory Plates = Factory(Document, "Plates", ConstructorC);

    private static FactoryOutput Ingots(params StoredFactory[] factories) =>
        Assert.Single(SnapshotAnalysis.ForFactory(Document, Smelting, TestWorldAnalysis, factories)!.Output);

    [Fact]
    public void What_leaves_for_a_machine_of_another_factory_goes_to_that_factory()
    {
        OutputDestination destination = Assert.Single(Ingots(Smelting, Plates).Destinations);

        Assert.Equal((OutputDestinationKind.Factory, Plates.Id, "Plates", 12.5), (destination.Kind, destination.FactoryId, destination.Factory, destination.PerMinute));
    }

    [Fact]
    public void What_leaves_for_a_machine_no_factory_has_goes_to_machines_of_no_factory()
    {
        OutputDestination destination = Assert.Single(Ingots(Smelting).Destinations);

        Assert.Equal((OutputDestinationKind.NoFactory, (Guid?)null, 12.5), (destination.Kind, destination.FactoryId, destination.PerMinute));
    }

    [Fact]
    public void Its_receivers_would_take_what_their_recipes_ask_for_at_100_percent_whatever_else_feeds_them()
    {
        // Constructor C asks 30 ingots a minute at 100% clock; at its 75%, 22.5. Smelter B brings 10 of those.
        Assert.Equal(22.5, Ingots(Smelting, Plates).ReceiversNeed);
    }

    [Fact]
    public void Its_receivers_get_the_rest_from_others_and_are_short_of_nothing_when_that_covers_their_need()
    {
        // Constructor C takes 22.5 a minute: 12.5 from smelter A, 10 from smelter B.
        FactoryOutput ingots = Ingots(Smelting, Plates);

        Assert.Equal((10.0, 0.0), (ingots.OthersBring, ingots.Short));
    }

    [Fact]
    public void What_its_output_blocked_machines_would_make_more_at_100_percent_cannot_leave()
    {
        Assert.Equal(30 - 12.5, Ingots(Smelting, Plates).CannotLeave);
    }

    [Fact]
    public void A_line_into_a_container_goes_to_storage_and_nothing_is_sunk()
    {
        StoredFactory ironPlates = Factory(Document, "Iron plates", SmelterA, SmelterB, ConstructorC);

        FactoryOutput plates = Assert.Single(SnapshotAnalysis.ForFactory(Document, ironPlates, TestWorldAnalysis, [ironPlates])!.Output);

        OutputDestination destination = Assert.Single(plates.Destinations);
        Assert.Equal((OutputDestinationKind.Storage, 15.0, 0.0), (destination.Kind, destination.PerMinute, plates.Sunk));
    }

    [Fact]
    public void A_generator_it_feeds_would_take_its_fuel()
    {
        // In the later save a packager unpacks turbofuel for the fuel generator: 250 MW of 2,000 MJ a cubic metre.
        StoredFactory unpacking = Factory(WithGenerators, "Unpacking", TurbofuelPackager);

        FactoryOutput turbofuel = SnapshotAnalysis.ForFactory(WithGenerators, unpacking, WithGeneratorsAnalysis, [unpacking])!.Output
            .Single(output => output.Item == "Desc_LiquidTurboFuel_C");

        Assert.Equal(7.5, turbofuel.ReceiversNeed);
    }

    [Fact]
    public void What_its_producers_make_for_its_own_machines_only_is_no_output()
    {
        // box → in → packager → (fuel → generator, cans → sink): the packager unpacks turbofuel for the factory's own
        // fuel generator, so only its empty canisters leave.
        var saved = new SavedWorld(
            new SaveInfo("Unpacking", 517554, new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc)),
            [
                new SavedLogistics(Saved("box"), "Build_StorageContainerMk1_C", LogisticsKind.StorageContainer)
                {
                    Stock = new Dictionary<string, decimal> { ["Desc_TurboFuel_C"] = 1_000 },
                },
                new SavedLogistics(Saved("in"), "Build_ConveyorBeltMk1_C", LogisticsKind.Belt),
                new SavedMachine(Saved("packager"), "Build_Packager_C", "Recipe_UnpackageTurboFuel_C", ClockSpeed.Percent(100), 1),
                new SavedLogistics(Saved("fuel"), "Build_PipelineMK2_C", LogisticsKind.Pipe),
                new SavedConsumer(Saved("generator"), "Build_GeneratorFuel_C") { Fuel = "Desc_LiquidTurboFuel_C", ClockSpeed = ClockSpeed.Percent(100) },
                new SavedLogistics(Saved("cans"), "Build_ConveyorBeltMk1_C", LogisticsKind.Belt),
                new SavedConsumer(Saved("sink"), "Build_ResourceSink_C"),
            ],
            [
                new Connection(Saved("box"), Saved("in")), new Connection(Saved("in"), Saved("packager")),
                new Connection(Saved("packager"), Saved("fuel")), new Connection(Saved("fuel"), Saved("generator")),
                new Connection(Saved("packager"), Saved("cans")), new Connection(Saved("cans"), Saved("sink")),
            ],
            []);
        FactoryOutput output = Assert.Single(OutputOf(saved, new TestAnalysis(), "packager", "generator"));

        Assert.Equal("Desc_FluidCanister_C", output.Item);
    }

    [Fact]
    public void What_goes_into_a_portal_goes_to_a_consumer_named_after_its_building()
    {
        // box → ore → smelter → ingots → portal.
        var saved = new SavedWorld(
            new SaveInfo("Portal", 517554, new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc)),
            [
                new SavedLogistics(Saved("box"), "Build_StorageContainerMk1_C", LogisticsKind.StorageContainer)
                {
                    Stock = new Dictionary<string, decimal> { ["Desc_OreIron_C"] = 1_000 },
                },
                new SavedLogistics(Saved("ore"), "Build_ConveyorBeltMk1_C", LogisticsKind.Belt),
                new SavedMachine(Saved("smelter"), "Build_SmelterMk1_C", "Recipe_IngotIron_C", ClockSpeed.Percent(100), 1),
                new SavedLogistics(Saved("ingots"), "Build_ConveyorBeltMk1_C", LogisticsKind.Belt),
                new SavedConsumer(Saved("portal"), "Build_Portal_C"),
            ],
            [
                new Connection(Saved("box"), Saved("ore")), new Connection(Saved("ore"), Saved("smelter")),
                new Connection(Saved("smelter"), Saved("ingots")), new Connection(Saved("ingots"), Saved("portal")),
            ],
            []);

        // The smelter runs at full speed: its 30 ingots a minute go into the portal.
        TestAnalysis analysis = new TestAnalysis().Running(Id("smelter")).Into(Id("ingots"), "Desc_IronIngot_C", 30);

        OutputDestination destination = Assert.Single(Assert.Single(OutputOf(saved, analysis, "smelter")).Destinations);

        Assert.Equal((OutputDestinationKind.Consumer, "Build_Portal_C"), (destination.Kind, destination.Building));
    }

    [Fact]
    public void What_its_own_machines_take_of_an_item_that_leaves_is_not_what_it_could_make_more()
    {
        // box → ore → smelter → ingots → split → (to constructor → constructor at 50% → plates → sink, spare → sink):
        // the smelter makes 30 ingots a minute, its own constructor takes 15, so 15 can leave at most.
        var saved = new SavedWorld(
            new SaveInfo("Own use", 517554, new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc)),
            [
                new SavedLogistics(Saved("box"), "Build_StorageContainerMk1_C", LogisticsKind.StorageContainer)
                {
                    Stock = new Dictionary<string, decimal> { ["Desc_OreIron_C"] = 1_000 },
                },
                new SavedLogistics(Saved("ore"), "Build_ConveyorBeltMk1_C", LogisticsKind.Belt),
                new SavedMachine(Saved("smelter"), "Build_SmelterMk1_C", "Recipe_IngotIron_C", ClockSpeed.Percent(100), 1),
                new SavedLogistics(Saved("ingots"), "Build_ConveyorBeltMk1_C", LogisticsKind.Belt),
                new SavedLogistics(Saved("split"), "Build_ConveyorAttachmentSplitter_C", LogisticsKind.Splitter),
                new SavedLogistics(Saved("to constructor"), "Build_ConveyorBeltMk1_C", LogisticsKind.Belt),
                new SavedMachine(Saved("constructor"), "Build_ConstructorMk1_C", "Recipe_IronPlate_C", ClockSpeed.Percent(50), 1),
                new SavedLogistics(Saved("plates"), "Build_ConveyorBeltMk1_C", LogisticsKind.Belt),
                new SavedConsumer(Saved("plate sink"), "Build_ResourceSink_C"),
                new SavedLogistics(Saved("spare"), "Build_ConveyorBeltMk1_C", LogisticsKind.Belt),
                new SavedConsumer(Saved("ingot sink"), "Build_ResourceSink_C"),
            ],
            [
                new Connection(Saved("box"), Saved("ore")), new Connection(Saved("ore"), Saved("smelter")),
                new Connection(Saved("smelter"), Saved("ingots")), new Connection(Saved("ingots"), Saved("split")),
                new Connection(Saved("split"), Saved("to constructor")), new Connection(Saved("to constructor"), Saved("constructor")),
                new Connection(Saved("constructor"), Saved("plates")), new Connection(Saved("plates"), Saved("plate sink")),
                new Connection(Saved("split"), Saved("spare")), new Connection(Saved("spare"), Saved("ingot sink")),
            ],
            []);

        FactoryOutput ingots = OutputOf(saved, new TestAnalysis(), "smelter", "constructor").Single(output => output.Item == "Desc_IronIngot_C");

        Assert.Equal(15, ingots.AtFullSpeed);
    }

    private static BuildableId Saved(string name) => BuildableId.Of(TestWorldSnapshot.Id(name));

    /// <summary>A hand-built world with its hand-built analysis, and the output of a factory of the named machines in it.</summary>
    private static IReadOnlyList<FactoryOutput> OutputOf(SavedWorld saved, TestAnalysis analysis, params string[] machines)
    {
        SnapshotDocument snapshot = SnapshotDocument.FromJson(SnapshotDocument.Create(saved, TestWorldSnapshot.Catalog).ToJson());
        StoredFactory factory = Factory(snapshot, "Hand-built", [.. machines.Select(TestWorldSnapshot.Id)]);
        return SnapshotAnalysis.ForFactory(snapshot, factory, analysis.Build(), [factory])!.Output;
    }

    [Fact]
    public void A_drone_port_its_belt_brings_all_the_fuel_its_drones_burn_is_short_of_nothing()
    {
        // A packager makes packaged turbofuel onto the belt into a drone port, where one drone refuels: 5/3 a minute.
        const string PackagedTurbofuel = "Desc_TurboFuel_C";
        string tank = Id("Build_PipeStorageTank_C_1"), pipe = Id("Build_Pipeline_C_1"), canisters = Id("Build_StorageContainerMk2_C_1");
        string canisterBelt = Id("Build_ConveyorBeltMk1_C_1"), packager = Id("Build_Packager_C_1"), fuelBelt = Id("Build_ConveyorBeltMk1_C_2");
        string port = Id("Build_DroneStation_C_1"), home = Id("Build_DroneStation_C_2");
        var saved = new SavedWorld(
            new SaveInfo("Drones", 517554, new DateTime(2026, 11, 26, 0, 0, 0, DateTimeKind.Utc)),
            [
                new SavedLogistics(BuildableId.Of(tank), "Build_PipeStorageTank_C", LogisticsKind.FluidBuffer)
                {
                    Stock = new Dictionary<string, decimal> { ["Desc_LiquidTurboFuel_C"] = 400 },
                },
                new SavedLogistics(BuildableId.Of(pipe), "Build_Pipeline_C", LogisticsKind.Pipe),
                new SavedLogistics(BuildableId.Of(canisters), "Build_StorageContainerMk2_C", LogisticsKind.StorageContainer)
                {
                    Stock = new Dictionary<string, decimal> { ["Desc_FluidCanister_C"] = 500 },
                },
                new SavedLogistics(BuildableId.Of(canisterBelt), "Build_ConveyorBeltMk1_C", LogisticsKind.Belt),
                new SavedMachine(BuildableId.Of(packager), "Build_Packager_C", "Recipe_PackagedTurboFuel_C", ClockSpeed.Percent(100), 1),
                new SavedLogistics(BuildableId.Of(fuelBelt), "Build_ConveyorBeltMk1_C", LogisticsKind.Belt),
                new SavedProvider(BuildableId.Of(port), "Build_DroneStation_C", Node: null, ClockSpeed.Percent(100)) { Burns = new HashSet<string> { PackagedTurbofuel } },
                new SavedConsumer(BuildableId.Of(home), "Build_DroneStation_C")
                {
                    Burns = new HashSet<string>(),
                    Drone = new SavedDroneRoute(BuildableId.Of(port), 3_000, 300),
                },
            ],
            [
                new Connection(BuildableId.Of(tank), BuildableId.Of(pipe)), new Connection(BuildableId.Of(pipe), BuildableId.Of(packager)),
                new Connection(BuildableId.Of(canisters), BuildableId.Of(canisterBelt)), new Connection(BuildableId.Of(canisterBelt), BuildableId.Of(packager)),
                new Connection(BuildableId.Of(packager), BuildableId.Of(fuelBelt)), new Connection(BuildableId.Of(fuelBelt), BuildableId.Of(port)),
            ],
            []);
        CatalogCopy catalog = TestWorldSnapshot.Catalog with
        {
            Recipes = [.. TestWorldSnapshot.Catalog.Recipes, new CatalogRecipe(
                "Recipe_PackagedTurboFuel_C", "Build_Packager_C", 6, [new("Desc_LiquidTurboFuel_C", 2), new("Desc_FluidCanister_C", 2)], [new(PackagedTurbofuel, 2)])],
            Items = [.. TestWorldSnapshot.Catalog.Items, new CatalogItem(PackagedTurbofuel, "Packaged Turbofuel", false)],
            // A trip of 3 km costs 24,000 + 6 × 6,000 MJ: 60,000 MJ every 5 minutes, of fuel with 7,200 MJ an item.
            DronePorts = [new CatalogDronePort("Build_DroneStation_C", 24_000, 6, [new(PackagedTurbofuel, 7_200)])],
        };
        SnapshotDocument snapshot = SnapshotDocument.FromJson(SnapshotDocument.Create(saved, catalog).ToJson());
        // The packager runs at the port's pace: the 5/3 a minute its drone burns goes over the belt.
        AnalysisDocument analysis = new TestAnalysis().Into(fuelBelt, PackagedTurbofuel, 5.0 / 3).Build();
        StoredFactory fuel = Factory(snapshot, "Drone fuel", packager);

        FactoryOutput output = Assert.Single(SnapshotAnalysis.ForFactory(snapshot, fuel, analysis, [fuel])!.Output);

        Assert.Equal((1.6667, 0.0, 0.0), (output.ReceiversNeed, output.OthersBring, output.Short));
    }
}
