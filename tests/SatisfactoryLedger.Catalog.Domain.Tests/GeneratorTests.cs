using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.Catalog.Domain.Tests;

/// <summary>Generators from the Docs file (BR-A8): how fast they burn a fuel, how much water they need, what waste they make.</summary>
public class GeneratorTests
{
    private static readonly ItemId UraniumRod = ItemId.Of("Desc_NuclearFuelRod_C");
    private static readonly ItemId PlutoniumRod = ItemId.Of("Desc_PlutoniumFuelRod_C");
    private static readonly ItemId Water = ItemId.Of("Desc_Water_C");
    private static readonly ItemId UraniumWaste = ItemId.Of("Desc_NuclearWaste_C");
    private static readonly ItemId Fuel = ItemId.Of("Desc_LiquidFuel_C");

    // As the Docs file has them: 2,500 MW, 1.6 litres of water per MJ; a uranium rod 750,000 MJ, 50 waste.
    private static Generator NuclearPlant() => Generator.Create(GeneratorId.Of("Build_GeneratorNuclear_C"), "Nuclear Power Plant", 2500, 1.6m,
    [
        new GeneratorFuel(UraniumRod, 750_000, Water, UraniumWaste, 50),
        new GeneratorFuel(PlutoniumRod, 1_500_000, Water, ItemId.Of("Desc_PlutoniumWaste_C"), 10),
    ]);

    // 250 MW; fuel has 0.75 MJ per litre, so 750 per cubic metre.
    private static Generator FuelGenerator() => Generator.Create(GeneratorId.Of("Build_GeneratorFuel_C"), "Fuel-Powered Generator", 250, 0,
        [new GeneratorFuel(Fuel, 750, null, null, 0)]);

    [Fact]
    public void A_nuclear_plant_burns_a_uranium_rod_every_five_minutes()
    {
        Assert.Equal(Rate.PerMinute(0.2m), NuclearPlant().BurnRate(UraniumRod));
    }

    [Fact]
    public void A_nuclear_plant_burning_uranium_makes_10_waste_a_minute()
    {
        Assert.Equal(Rate.PerMinute(10), NuclearPlant().ByproductRate(UraniumRod));
    }

    [Fact]
    public void A_plutonium_rod_lasts_twice_as_long_and_leaves_1_waste_a_minute()
    {
        Generator plant = NuclearPlant();

        Assert.Equal((Rate.PerMinute(0.1m), Rate.PerMinute(1)), (plant.BurnRate(PlutoniumRod), plant.ByproductRate(PlutoniumRod)));
    }

    [Fact]
    public void A_nuclear_plant_needs_240_cubic_metres_of_water_a_minute()
    {
        Assert.Equal(Rate.PerMinute(240), NuclearPlant().SupplementalRate);
    }

    [Fact]
    public void A_fuel_generator_burns_20_cubic_metres_of_fuel_a_minute()
    {
        Assert.Equal(Rate.PerMinute(20), FuelGenerator().BurnRate(Fuel));
    }

    [Fact]
    public void A_generator_without_a_supplemental_fluid_needs_none()
    {
        Assert.Equal(Rate.Zero, FuelGenerator().SupplementalRate);
    }

    [Fact]
    public void A_fuel_without_waste_leaves_none()
    {
        Assert.Equal(Rate.Zero, FuelGenerator().ByproductRate(Fuel));
    }

    [Fact]
    public void A_generator_cannot_burn_what_it_has_no_fuel_entry_for()
    {
        Assert.Throws<ArgumentException>(() => FuelGenerator().BurnRate(UraniumRod));
    }

    [Fact]
    public void A_generator_produces_power()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Generator.Create(GeneratorId.Of("Build_GeneratorFuel_C"), "Fuel-Powered Generator", 0, 0, []));
    }

    [Fact]
    public void A_fuel_has_energy()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GeneratorFuel(Fuel, 0, null, null, 0));
    }

    [Fact]
    public void A_generator_from_a_newer_game_file_takes_its_power_and_fuels()
    {
        Generator stored = FuelGenerator();
        stored.MarkNoLongerInGame();

        stored.UpdateFromGame(Generator.Create(GeneratorId.Of("Build_GeneratorFuel_C"), "Fuel-Powered Generator", 300, 0,
            [new GeneratorFuel(Fuel, 750, null, null, 0)]));

        Assert.Equal((Rate.PerMinute(24), true), (stored.BurnRate(Fuel), stored.IsInGame));
    }
}
