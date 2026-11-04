using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.Catalog.Domain.Tests;

public class ExtractorTests
{
    private static Extractor MinerMk1() => Extractor.Create(ExtractorId.Of("Build_MinerMk1_C"), "Miner Mk.1", ItemForm.Solid, 1, 1);

    [Fact]
    public void An_extractor_id_cannot_be_blank()
    {
        Assert.Throws<ArgumentException>(() => ExtractorId.Of(" "));
    }

    [Fact]
    public void A_miner_mk1_extracts_60_per_minute_on_a_normal_node()
    {
        Assert.Equal(Rate.PerMinute(60), MinerMk1().ExtractionRate);
    }

    [Fact]
    public void A_miner_mk3_extracts_one_item_every_quarter_second_which_is_240_per_minute()
    {
        var minerMk3 = Extractor.Create(ExtractorId.Of("Build_MinerMk3_C"), "Miner Mk.3", ItemForm.Solid, 1, 0.25m);

        Assert.Equal(Rate.PerMinute(240), minerMk3.ExtractionRate);
    }

    [Fact]
    public void A_water_extractor_extracts_120_cubic_metres_per_minute()
    {
        var waterExtractor = Extractor.Create(ExtractorId.Of("Build_WaterPump_C"), "Water Extractor", ItemForm.Fluid, 2, 1);

        Assert.Equal(Rate.PerMinute(120), waterExtractor.ExtractionRate);
    }

    [Fact]
    public void An_extractor_needs_a_name()
    {
        Assert.Throws<ArgumentException>(() => Extractor.Create(ExtractorId.Of("Build_MinerMk1_C"), " ", ItemForm.Solid, 1, 1));
    }

    [Fact]
    public void An_extractor_extracts_a_positive_amount_per_cycle()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Extractor.Create(ExtractorId.Of("Build_MinerMk1_C"), "Miner Mk.1", ItemForm.Solid, 0, 1));
    }

    [Fact]
    public void An_extractor_has_a_positive_cycle_time()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Extractor.Create(ExtractorId.Of("Build_MinerMk1_C"), "Miner Mk.1", ItemForm.Solid, 1, 0));
    }

    [Fact]
    public void Extractors_with_the_same_id_are_equal()
    {
        var older = MinerMk1();
        var newer = Extractor.Create(ExtractorId.Of("Build_MinerMk1_C"), "Miner Mk.1 (renamed)", ItemForm.Solid, 2, 1);

        Assert.Equal(older, newer);
        Assert.Equal(older.GetHashCode(), newer.GetHashCode());
    }

    [Fact]
    public void Updating_an_extractor_from_the_game_takes_the_game_data()
    {
        var stored = MinerMk1();
        stored.MarkNoLongerInGame();

        stored.UpdateFromGame(Extractor.Create(ExtractorId.Of("Build_MinerMk1_C"), "Miner Mk.1 (new)", ItemForm.Solid, 1, 0.5m));

        Assert.Equal("Miner Mk.1 (new)", stored.Name);
        Assert.Equal(Rate.PerMinute(120), stored.ExtractionRate);
        Assert.True(stored.IsInGame);
    }

    [Fact]
    public void An_extractor_cannot_be_updated_from_a_different_extractor()
    {
        var waterExtractor = Extractor.Create(ExtractorId.Of("Build_WaterPump_C"), "Water Extractor", ItemForm.Fluid, 2, 1);

        Assert.Throws<ArgumentException>(() => MinerMk1().UpdateFromGame(waterExtractor));
    }

    [Fact]
    public void An_extractor_marked_no_longer_in_game_is_not_in_the_game()
    {
        var miner = MinerMk1();

        miner.MarkNoLongerInGame();

        Assert.False(miner.IsInGame);
    }
}
