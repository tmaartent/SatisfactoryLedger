using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Analysis;
using SatisfactoryLedger.World.Contracts.Analysis;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Factories;
using SatisfactoryLedger.World.SaveFile;
using SatisfactoryLedger.World.Simulation;
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
        AnalysisDocument analysis = AnalysisDocument.FromJson(AnalysisDocument.Compute(WithGenerators.ToFlowNetwork()).ToJson());
        StoredFactory unpacking = Factory(WithGenerators, "Unpacking", TurbofuelPackager);

        FactoryOutput turbofuel = SnapshotAnalysis.ForFactory(WithGenerators, unpacking, analysis, [unpacking])!.Output
            .Single(output => output.Item == "Desc_LiquidTurboFuel_C");

        Assert.Equal(7.5, turbofuel.ReceiversNeed);
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

        OutputDestination destination = Assert.Single(Assert.Single(OutputOf(saved, "smelter")).Destinations);

        Assert.Equal((OutputDestinationKind.Consumer, "Build_Portal_C"), (destination.Kind, destination.Building));
    }

    private static BuildableId Saved(string name) => BuildableId.Of(TestWorldSnapshot.Id(name));

    /// <summary>A hand-built world, analysed, and the output of a factory of the named machines in it.</summary>
    private static IReadOnlyList<FactoryOutput> OutputOf(SavedWorld saved, params string[] machines)
    {
        SnapshotDocument snapshot = SnapshotDocument.FromJson(SnapshotDocument.Create(saved, TestWorldSnapshot.Catalog).ToJson());
        AnalysisDocument analysis = AnalysisDocument.FromJson(AnalysisDocument.Compute(snapshot.ToFlowNetwork()).ToJson());
        StoredFactory factory = Factory(snapshot, "Hand-built", [.. machines.Select(TestWorldSnapshot.Id)]);
        return SnapshotAnalysis.ForFactory(snapshot, factory, analysis, [factory])!.Output;
    }
}
