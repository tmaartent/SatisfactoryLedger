using SatisfactoryLedger.World.Analysis;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.Domain.Flow.Objects;
using SatisfactoryLedger.World.Contracts.Analysis;
using SatisfactoryLedger.World.SaveFile;
using SatisfactoryLedger.World.Snapshots;

using static SatisfactoryLedger.World.Tests.TestWorldSnapshot;
using MachineLimit = SatisfactoryLedger.World.Contracts.Analysis.MachineLimit;

namespace SatisfactoryLedger.World.Tests;

/// <summary>
/// Generator findings (BR-A15) in the test world saved with a nuclear plant, a coal and a fuel generator, from its stored
/// analysis. How the generators run is the solver's call: tested end to end in World.Simulation.Tests.
/// </summary>
public class GeneratorFindingsTests
{
    private static readonly AnalysisResponse Analysis = SnapshotAnalysis.For(WithGenerators, WithGeneratorsAnalysis);

    [Fact]
    public void Generators_are_not_machines()
    {
        // Five machines as before, and the packager added to unpack turbofuel; not the three generators.
        Assert.Equal(6, Analysis.Summary.Machines);
        Assert.DoesNotContain(Analysis.Machines, machine => machine.Id == NuclearPlant);
    }

    [Fact]
    public void Every_generator_burning_a_known_fuel_is_a_generator_finding_with_its_fuel()
    {
        Assert.Equal(
            new Dictionary<string, string>
            {
                [NuclearPlant] = "Desc_NuclearFuelRod_C",
                [CoalGenerator] = "Desc_CompactedCoal_C",
                [FuelGenerator] = "Desc_LiquidTurboFuel_C",
            },
            Analysis.Generators.ToDictionary(generator => generator.Id, generator => generator.Fuel));
    }

    [Fact]
    public void Generators_are_listed_worst_first()
    {
        Assert.Equal(
            Analysis.Generators.Select(generator => generator.Utilisation).Order(),
            Analysis.Generators.Select(generator => generator.Utilisation));
    }

    [Fact]
    public void The_generator_summary_counts_its_generators_by_how_they_run()
    {
        // The nuclear plant and the coal generator run; the fuel generator starves.
        Assert.Equal(new GeneratorSummary(3, 2, 1, 0, 0), Analysis.GeneratorSummary);
    }

    [Fact]
    public void A_generator_whose_fuel_is_not_known_counts_as_fuel_unknown()
    {
        var saved = new SavedWorld(
            new SaveInfo("Empty slot", 517554, new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc)),
            [new SavedConsumer(BuildableId.Of(Id("Build_GeneratorFuel_C_1")), "Build_GeneratorFuel_C")],
            [],
            []);
        SnapshotDocument snapshot = SnapshotDocument.FromJson(SnapshotDocument.Create(saved, TestWorldSnapshot.Catalog).ToJson());

        // Not simulated: the analysis has nothing to say about it.
        AnalysisResponse analysis = SnapshotAnalysis.For(snapshot, new TestAnalysis().Build());

        Assert.Equal(new GeneratorSummary(1, 0, 0, 0, 1), analysis.GeneratorSummary);
    }
}
