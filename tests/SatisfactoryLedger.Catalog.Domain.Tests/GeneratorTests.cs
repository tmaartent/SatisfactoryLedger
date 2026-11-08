namespace SatisfactoryLedger.Catalog.Domain.Tests;

/// <summary>
/// Generators from the Docs file (BR-A8): their power, the water they need per megajoule, and per fuel its energy and
/// waste. The World module turns these into rates per minute (<c>GeneratorSnapshotTests</c>).
/// </summary>
public class GeneratorTests
{
    private static readonly ItemId Fuel = ItemId.Of("Desc_LiquidFuel_C");

    // 250 MW; fuel has 0.75 MJ per litre, so 750 per cubic metre.
    private static Generator FuelGenerator() => Generator.Create(GeneratorId.Of("Build_GeneratorFuel_C"), "Fuel-Powered Generator", 250, 0,
        [new GeneratorFuel(Fuel, 750, null, null, 0)]);

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
            [new GeneratorFuel(Fuel, 2_000, null, null, 0)]));

        Assert.Equal((300m, 2_000m, true), (stored.PowerMegawatts, Assert.Single(stored.Fuels).EnergyMegajoules, stored.IsInGame));
    }
}
