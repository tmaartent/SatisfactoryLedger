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

    [Fact]
    public void A_generator_asks_its_fuel_and_its_water_at_full_power()
    {
        Buildable generator = new Consumer(Id, generation: new Generation(
            Items.UraniumFuelRod, Rate.PerMinute(0.2m), Items.Water, Rate.PerMinute(240), Items.UraniumWaste, Rate.PerMinute(10)));

        Assert.Equal(
            new Dictionary<ItemId, Rate> { [Items.UraniumFuelRod] = Rate.PerMinute(0.2m), [Items.Water] = Rate.PerMinute(240) },
            generator.Demand);
    }

    [Fact]
    public void A_generator_that_needs_no_water_asks_only_its_fuel()
    {
        Buildable generator = new Consumer(Id, generation: new Generation(Items.Coal, Rate.PerMinute(15), Items.Water, Rate.Zero, null, Rate.Zero));

        Assert.Equal(new Dictionary<ItemId, Rate> { [Items.Coal] = Rate.PerMinute(15) }, generator.Demand);
        Assert.Equal(new HashSet<ItemId> { Items.Coal }, generator.Consumes);
    }

    [Fact]
    public void A_generator_makes_its_waste()
    {
        Buildable generator = new Consumer(Id, generation: new Generation(
            Items.UraniumFuelRod, Rate.PerMinute(0.2m), Items.Water, Rate.PerMinute(240), Items.UraniumWaste, Rate.PerMinute(10)));

        Assert.Equal(new Dictionary<ItemId, Rate> { [Items.UraniumWaste] = Rate.PerMinute(10) }, generator.Production);
    }

    [Fact]
    public void A_generator_makes_its_waste_only_when_some_leaves_it()
    {
        Buildable leaving = new Consumer(Id, generation: new Generation(
            Items.UraniumFuelRod, Rate.PerMinute(0.2m), Items.Water, Rate.PerMinute(240), Items.UraniumWaste, Rate.PerMinute(10)));
        Buildable leavingNone = new Consumer(Id, generation: new Generation(
            Items.UraniumFuelRod, Rate.PerMinute(0.2m), Items.Water, Rate.PerMinute(240), Items.UraniumWaste, Rate.Zero));

        Assert.True(leaving.Makes(Items.UraniumWaste));
        Assert.False(leavingNone.Makes(Items.UraniumWaste));
    }

    [Fact]
    public void A_generator_burning_a_known_fuel_accepts_only_its_fuel_and_its_water()
    {
        Buildable generator = new Consumer(Id, generation: new Generation(
            Items.UraniumFuelRod, Rate.PerMinute(0.2m), Items.Water, Rate.PerMinute(240), Items.UraniumWaste, Rate.PerMinute(10)));

        Assert.True(generator.Accepts(Items.UraniumFuelRod));
        Assert.True(generator.Accepts(Items.Water));
        Assert.False(generator.Accepts(Items.IronPlate));
    }

    [Fact]
    public void A_consumer_without_known_generation_accepts_every_item()
    {
        Buildable sink = new Consumer(Id);
        Buildable generator = new Consumer(Id) { IsGenerator = true };
        Buildable port = new Consumer(Id, new HashSet<ItemId> { Items.Battery }, fuelBurn: new FuelBurn.Known(Rate.PerMinute(2)));

        Assert.True(sink.Accepts(Items.IronPlate));
        Assert.True(generator.Accepts(Items.IronPlate));
        Assert.True(port.Accepts(Items.IronPlate));
    }

    [Fact]
    public void A_generator_holds_its_fuel_as_input_stock()
    {
        Buildable generator = new Consumer(Id, generation: new Generation(Items.Coal, Rate.PerMinute(15), Items.Water, Rate.Zero, null, Rate.Zero))
        {
            InputStock = new Dictionary<ItemId, decimal> { [Items.Coal] = 50 },
        };

        Assert.Equal(new Dictionary<ItemId, decimal> { [Items.Coal] = 50 }, generator.InputStock);
    }

    [Fact]
    public void A_belt_holds_no_input_stock()
    {
        Buildable belt = new Logistics(Id, LogisticsKind.Belt, Rate.PerMinute(60));

        Assert.Empty(belt.InputStock);
    }

    [Fact]
    public void A_generator_whose_fuel_is_not_known_asks_and_makes_nothing_in_particular()
    {
        Buildable generator = new Consumer(Id) { IsGenerator = true };

        Assert.Empty(generator.Demand);
        Assert.Empty(generator.Production);
    }

    [Fact]
    public void A_drone_port_that_loads_asks_the_fuel_its_drones_burn()
    {
        Buildable port = new Consumer(Id, new HashSet<ItemId> { Items.Battery }, fuelBurn: new FuelBurn.Known(Rate.PerMinute(2)));

        Assert.Equal(new Dictionary<ItemId, Rate> { [Items.Battery] = Rate.PerMinute(2) }, port.Demand);
    }

    [Fact]
    public void A_drone_port_that_unloads_asks_the_fuel_its_drones_burn()
    {
        Buildable port = new Provider(Id, burns: new HashSet<ItemId> { Items.Battery }, fuelBurn: new FuelBurn.Known(Rate.PerMinute(2)));

        Assert.Equal(new Dictionary<ItemId, Rate> { [Items.Battery] = Rate.PerMinute(2) }, port.Demand);
    }

    [Fact]
    public void A_drone_port_that_loads_where_no_drone_refuels_asks_and_consumes_none_of_its_fuel()
    {
        Buildable port = new Consumer(Id, new HashSet<ItemId> { Items.Battery }, fuelBurn: new FuelBurn.NoDroneRefuels());

        Assert.Empty(port.Demand);
        Assert.Empty(port.Consumes);
    }

    [Fact]
    public void A_drone_port_that_unloads_where_no_drone_refuels_asks_and_consumes_none_of_its_fuel()
    {
        Buildable port = new Provider(Id, burns: new HashSet<ItemId> { Items.Battery }, fuelBurn: new FuelBurn.NoDroneRefuels());

        Assert.Empty(port.Demand);
        Assert.Empty(port.Consumes);
    }

    [Fact]
    public void A_truck_station_consumes_its_fuel_at_a_rate_that_is_not_known()
    {
        Buildable station = new Provider(Id, burns: new HashSet<ItemId> { Items.Coal });

        Assert.Equal(new HashSet<ItemId> { Items.Coal }, station.Consumes);
        Assert.Empty(station.Demand);
    }

    [Fact]
    public void A_piece_of_logistics_asks_and_makes_nothing()
    {
        Buildable belt = new Logistics(Id, LogisticsKind.Belt, Rate.PerMinute(60));

        Assert.Empty(belt.Demand);
        Assert.Empty(belt.Production);
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
