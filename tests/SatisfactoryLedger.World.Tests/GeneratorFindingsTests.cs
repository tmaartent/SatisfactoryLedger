using SatisfactoryLedger.World.Analysis;
using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.Domain.Flow.Objects;
using SatisfactoryLedger.World.Contracts.Analysis;

using static SatisfactoryLedger.World.Tests.TestWorldSnapshot;
using SatisfactoryLedger.World.Simulation;
using MachineLimit = SatisfactoryLedger.World.Contracts.Analysis.MachineLimit;

namespace SatisfactoryLedger.World.Tests;

/// <summary>Generator findings (BR-A15) in the test world saved with a nuclear plant, a coal and a fuel generator.</summary>
public class GeneratorFindingsTests
{
    private static readonly AnalysisResponse Analysis = SnapshotAnalysis.For(
        WithGenerators, AnalysisDocument.FromJson(AnalysisDocument.Compute(WithGenerators.ToFlowNetwork()).ToJson()));

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

    private static GeneratorFinding Generator(string id) => Assert.Single(Analysis.Generators, generator => generator.Id == id);

    [Fact]
    public void A_nuclear_plant_fed_rods_and_water_whose_waste_can_leave_runs_at_full_power()
    {
        Assert.Equal((1.0, MachineLimit.None), (Generator(NuclearPlant).Utilisation, Generator(NuclearPlant).Limit));
    }

    [Fact]
    public void A_coal_generator_fed_compacted_coal_and_water_runs_at_full_power()
    {
        Assert.Equal((1.0, MachineLimit.None), (Generator(CoalGenerator).Utilisation, Generator(CoalGenerator).Limit));
    }

    [Fact]
    public void A_fuel_generator_starves_when_the_packager_unpacking_its_turbofuel_cannot_get_rid_of_the_empty_canisters()
    {
        // The packager's only output is the turbofuel pipe: its canisters pile up (12 at save time) until it stops.
        MachineFinding packager = Assert.Single(Analysis.Machines, machine => machine.Recipe == "Recipe_UnpackageTurboFuel_C");

        Assert.Equal((MachineLimit.OutputBlocked, "Desc_FluidCanister_C"), (packager.Limit, packager.LimitingItem));
        Assert.Equal((MachineLimit.Starved, "Desc_LiquidTurboFuel_C"), (Generator(FuelGenerator).Limit, Generator(FuelGenerator).LimitingItem));
    }

    [Fact]
    public void A_machine_whose_output_cannot_leave_names_every_product_that_cannot_with_the_share_that_can()
    {
        MachineFinding packager = Assert.Single(Analysis.Machines, machine => machine.Recipe == "Recipe_UnpackageTurboFuel_C");

        Assert.Contains("Desc_FluidCanister_C", packager.BlockedOn.Keys);
        Assert.All(packager.BlockedOn.Values, share => Assert.InRange(share, 0, 0.999));
    }

    [Fact]
    public void A_starved_generator_names_the_fuel_it_is_short_of()
    {
        Assert.Contains("Desc_LiquidTurboFuel_C", Generator(FuelGenerator).StarvedOf.Keys);
    }
}
