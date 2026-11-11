using SatisfactoryLedger.World.Analysis;
using SatisfactoryLedger.World.Factories;
using SatisfactoryLedger.World.Contracts.Analysis;
using SatisfactoryLedger.World.Simulation;

using static SatisfactoryLedger.World.Tests.TestWorldSnapshot;

namespace SatisfactoryLedger.World.Tests;

/// <summary>
/// A factory with a generator among its machines (BR-W2, decided 2026-11-11), in the test world saved with a nuclear
/// plant: rods from a container, water from a pipe network, its waste on a belt into a container.
/// </summary>
public class GeneratorFactoryTests
{
    private static readonly StoredFactory Power = new()
    {
        Id = Guid.CreateVersion7(),
        World = "Development",
        Name = "Power",
        Machines = [StoredMachine.From(WithGenerators.Buildables.Single(buildable => buildable.Id == NuclearPlant))],
    };

    private static readonly FactoryAnalysisResponse Analysis = SnapshotAnalysis.ForFactory(
        WithGenerators, Power, AnalysisDocument.FromJson(AnalysisDocument.Compute(WithGenerators.ToFlowNetwork()).ToJson()), [Power])!;

    [Fact]
    public void A_factory_of_a_nuclear_plant_is_traced_with_the_plant_as_its_machine()
    {
        FactoryTrace trace = FactoryTraces.TraceAll(WithGenerators, [Power]).Single();

        Assert.True(trace.Traceable);
        Assert.Equal([NuclearPlant], trace.Machines.Select(machine => machine.Id));
        Assert.Empty(trace.Missing);
    }

    [Fact]
    public void Its_generators_are_its_generator_findings()
    {
        Assert.Equal([NuclearPlant], Analysis.Generators.Select(generator => generator.Id));
    }

    [Fact]
    public void It_says_what_power_it_makes_now_and_at_100_percent()
    {
        // A nuclear plant makes 2500 MW at 100% clock; this one runs at full power.
        Assert.Equal(new FactoryPower(2500, 2500), Analysis.Power);
    }

    [Fact]
    public void The_waste_its_plant_leaves_is_its_output()
    {
        // 0.2 rods a minute leave 10 waste a minute.
        FactoryOutput waste = Assert.Single(Analysis.Output);
        Assert.Equal(("Desc_NuclearWaste_C", 10.0, 10.0), (waste.Item, waste.Now, waste.AtFullSpeed));
    }

    [Fact]
    public void A_factory_without_generators_makes_no_power()
    {
        StoredFactory plates = new()
        {
            Id = Guid.CreateVersion7(),
            World = "Development",
            Name = "Iron plates",
            Machines = [.. new[] { SmelterA, SmelterB, ConstructorC }.Select(id => StoredMachine.From(Document.Buildables.Single(buildable => buildable.Id == id)))],
        };

        Assert.Null(SnapshotAnalysis.ForFactory(Document, plates, TestWorldAnalysis, [plates])!.Power);
    }
}
