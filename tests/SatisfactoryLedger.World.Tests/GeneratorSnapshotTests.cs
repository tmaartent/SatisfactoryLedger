using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Snapshots;

using static SatisfactoryLedger.World.Tests.TestWorldSnapshot;

namespace SatisfactoryLedger.World.Tests;

/// <summary>Generators in a stored snapshot (BR-A8): the fuel they burn, and what that takes and leaves per minute.</summary>
public class GeneratorSnapshotTests
{
    private static Generation GenerationOf(string id) =>
        Assert.IsType<Consumer>(WithGenerators.ToWorldGraph().Buildables.Single(buildable => buildable.Id == BuildableId.Of(id))).Generation!;

    [Fact]
    public void A_snapshot_copies_the_generators_it_has()
    {
        Assert.Equal(["Build_GeneratorCoal_C", "Build_GeneratorFuel_C", "Build_GeneratorNuclear_C"], WithGenerators.Generators!.Select(generator => generator.Id));
    }

    [Fact]
    public void A_nuclear_plant_on_uranium_burns_a_rod_every_five_minutes_needs_240_cubic_metres_of_water_and_leaves_10_waste()
    {
        Generation plant = GenerationOf(NuclearPlant);

        Assert.Equal(
            (ItemId.Of("Desc_NuclearFuelRod_C"), Rate.PerMinute(0.2m), Rate.PerMinute(240), ItemId.Of("Desc_NuclearWaste_C"), Rate.PerMinute(10)),
            (plant.Fuel, plant.Burns, plant.SupplementalNeeded, plant.Byproduct, plant.Leaves));
    }

    [Fact]
    public void A_coal_generator_on_compacted_coal_burns_fifty_sevenths_a_minute_exactly()
    {
        // 75 MW for 630 MJ per item: 75 × 60 ÷ 630 = 50/7.
        Assert.Equal(Rate.Of(50, 7), GenerationOf(CoalGenerator).Burns);
    }

    [Fact]
    public void A_fuel_generator_on_turbofuel_burns_7_and_a_half_cubic_metres_a_minute_and_needs_no_water()
    {
        Generation generator = GenerationOf(FuelGenerator);

        Assert.Equal((Rate.PerMinute(7.5m), (ItemId?)null), (generator.Burns, generator.Supplemental));
    }

    [Fact]
    public void Water_and_turbofuel_are_fluids_of_the_snapshot()
    {
        Assert.Superset(new HashSet<string> { "Desc_Water_C", "Desc_LiquidTurboFuel_C" }, WithGenerators.Fluids.ToHashSet());
    }

    [Fact]
    public void A_generator_burning_a_fuel_the_catalog_has_no_entry_for_takes_everything_like_before()
    {
        SnapshotDocument withoutTurbofuel = SnapshotDocument.FromJson(SnapshotDocument.Create(
            SaveFile.SaveFileReader.Read(File.OpenRead("Fixtures/Generators.sav")),
            TestWorldSnapshot.Catalog with { Generators = [.. TestWorldSnapshot.Catalog.Generators.Where(generator => generator.Id != "Build_GeneratorFuel_C")] }).ToJson());

        Assert.Null(Assert.IsType<Consumer>(withoutTurbofuel.ToWorldGraph().Buildables.Single(buildable => buildable.Id == BuildableId.Of(FuelGenerator))).Generation);
    }
}
