using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.World.Domain.Tests;

/// <summary>What buildables ask, make, offer and carry per minute (BR-A1, BR-A2, BR-A3, BR-W9).</summary>
public class BuildableRatesTests
{
    private static readonly BuildableId Id = BuildableId.Of("Build_ConstructorMk1_C_1");

    private static readonly Recipe IronPlate = new(
        new Dictionary<ItemId, Rate> { [Items.IronIngot] = Rate.PerMinute(30) },
        new Dictionary<ItemId, Rate> { [Items.IronPlate] = Rate.PerMinute(20) });

    [Fact]
    public void A_machine_with_somersloops_makes_a_bigger_batch_each_cycle()
    {
        var machine = new Machine(Id, IronPlate with { Batches = new Dictionary<ItemId, decimal> { [Items.IronPlate] = 2 } }, ClockSpeed.Percent(100), productionBoost: 2);

        Assert.Equal(4, machine.Batches[Items.IronPlate]);
    }

    [Fact]
    public void A_machine_asks_its_recipe_rate_times_its_clock_speed()
    {
        var machine = new Machine(Id, IronPlate, ClockSpeed.Percent(150));

        Assert.Equal(Rate.PerMinute(45), machine.Demand[Items.IronIngot]);
    }

    [Fact]
    public void A_machine_makes_its_recipe_rate_times_its_clock_speed()
    {
        var machine = new Machine(Id, IronPlate, ClockSpeed.Percent(50));

        Assert.Equal(Rate.PerMinute(10), machine.Production[Items.IronPlate]);
    }

    [Fact]
    public void A_somersloop_boost_raises_what_a_machine_makes()
    {
        var machine = new Machine(Id, IronPlate, ClockSpeed.Percent(100), productionBoost: 2);

        Assert.Equal(Rate.PerMinute(40), machine.Production[Items.IronPlate]);
    }

    [Fact]
    public void A_somersloop_boost_leaves_what_a_machine_asks()
    {
        var machine = new Machine(Id, IronPlate, ClockSpeed.Percent(100), productionBoost: 2);

        Assert.Equal(Rate.PerMinute(30), machine.Demand[Items.IronIngot]);
    }

    [Fact]
    public void A_machine_whose_recipe_isnt_known_asks_and_makes_nothing()
    {
        var machine = new Machine(Id, recipe: null, ClockSpeed.Percent(100));

        Assert.Empty(machine.Demand);
        Assert.Empty(machine.Production);
    }

    [Theory]
    [InlineData(Purity.Impure, 30)]
    [InlineData(Purity.Normal, 60)]
    [InlineData(Purity.Pure, 120)]
    public void An_extractor_offers_its_rate_times_the_purity_of_its_node(Purity purity, int offers)
    {
        ItemOffer offer = ItemOffer.Extracting(Items.IronOre, Rate.PerMinute(60), purity, ClockSpeed.Percent(100));

        Assert.Equal(Rate.PerMinute(offers), offer.PerMinute);
    }

    [Fact]
    public void An_overclocked_extractor_offers_more()
    {
        ItemOffer offer = ItemOffer.Extracting(Items.IronOre, Rate.PerMinute(120), Purity.Pure, ClockSpeed.Percent(250));

        Assert.Equal(Rate.PerMinute(600), offer.PerMinute);
    }
}
