using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Analysis;
using SatisfactoryLedger.World.Contracts.Analysis;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.SaveFile;
using SatisfactoryLedger.World.Snapshots;

using static SatisfactoryLedger.World.Tests.TestWorldSnapshot;

namespace SatisfactoryLedger.World.Simulation.Tests;

/// <summary>
/// End to end: what the solver decides, seen through the findings (issue #47). A save, stored as a snapshot, through the
/// solver and into the findings: how generators run (BR-A15), and machines on stock (BR-A17). The findings' own rules
/// are tested in World.Tests on a hand-built analysis.
/// </summary>
public class FindingsEndToEndTests
{
    private static readonly AnalysisResponse Generators = Analysed(WithGenerators);

    private static AnalysisResponse Analysed(SnapshotDocument snapshot) =>
        SnapshotAnalysis.For(snapshot, AnalysisDocument.FromJson(AnalysisDocument.Compute(snapshot.ToFlowNetwork()).ToJson()));

    private static GeneratorFinding Generator(string id) => Assert.Single(Generators.Generators, generator => generator.Id == id);

    private static MachineFinding Packager() => Assert.Single(Generators.Machines, machine => machine.Recipe == "Recipe_UnpackageTurboFuel_C");

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
        Assert.Equal((MachineLimit.OutputBlocked, "Desc_FluidCanister_C"), (Packager().Limit, Packager().LimitingItem));
        Assert.Equal((MachineLimit.Starved, "Desc_LiquidTurboFuel_C"), (Generator(FuelGenerator).Limit, Generator(FuelGenerator).LimitingItem));
    }

    [Fact]
    public void A_machine_whose_output_cannot_leave_names_every_product_that_cannot_with_the_share_that_can()
    {
        Assert.Contains("Desc_FluidCanister_C", Packager().BlockedOn.Keys);
        Assert.All(Packager().BlockedOn.Values, share => Assert.InRange(share, 0, 0.999));
    }

    [Fact]
    public void A_starved_generator_names_the_fuel_it_is_short_of()
    {
        Assert.Contains("Desc_LiquidTurboFuel_C", Generator(FuelGenerator).StarvedOf.Keys);
    }

    // ---- Machines on stock (BR-A17): a smelter (30 ore a minute) that nothing brings ore to --------------------------

    private const string Ore = "Desc_OreIron_C";
    private static readonly string Holding = Id("Build_SmelterMk1_C_1");

    private static MachineFinding SmelterHolding(decimal ore)
    {
        var saved = new SavedWorld(
            new SaveInfo("Stock", 517554, new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc)),
            [
                new SavedMachine(BuildableId.Of(Holding), "Build_SmelterMk1_C", "Recipe_IngotIron_C", ClockSpeed.Percent(100), 1)
                {
                    InputContents = new Dictionary<string, decimal> { [Ore] = ore },
                },
                new SavedLogistics(BuildableId.Of(Id("out")), "Build_ConveyorBeltMk1_C", LogisticsKind.Belt),
                new SavedConsumer(BuildableId.Of(Id("sink")), "Build_ResourceSink_C"),
            ],
            [new Connection(BuildableId.Of(Holding), BuildableId.Of(Id("out"))), new Connection(BuildableId.Of(Id("out")), BuildableId.Of(Id("sink")))],
            []);
        AnalysisResponse analysis = Analysed(SnapshotDocument.FromJson(SnapshotDocument.Create(saved, World.Tests.TestWorldSnapshot.Catalog).ToJson()));
        return Assert.Single(analysis.Machines, machine => machine.Id == Holding);
    }

    [Fact]
    public void A_machine_holding_half_an_hour_of_its_input_runs_at_full_speed_on_that_stock()
    {
        // 900 ore at 30 a minute.
        MachineFinding smelter = SmelterHolding(900);

        Assert.Equal((1.0, MachineLimit.None), (smelter.Utilisation, smelter.Limit));
    }

    [Fact]
    public void A_machine_holding_less_is_starved_like_before()
    {
        MachineFinding smelter = SmelterHolding(899);

        Assert.Equal((MachineLimit.Starved, (string?)null), (smelter.Limit, smelter.OnStock));
    }

    [Fact]
    public void A_starved_machine_names_every_input_it_is_short_of_with_the_share_it_is_offered()
    {
        MachineFinding smelter = SmelterHolding(899);

        Assert.Equal(new Dictionary<string, double> { [Ore] = 0 }, smelter.StarvedOf);
        Assert.Empty(smelter.BlockedOn);
    }
}
