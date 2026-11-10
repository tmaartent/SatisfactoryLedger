using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.Domain.Flow.Objects;

namespace SatisfactoryLedger.World.Domain.Tests;

/// <summary>Which kind of flow node each buildable of the world becomes, with its rates (BR-A1 to BR-A9).</summary>
public class WorldFlowTests
{
    private static readonly ItemId Water = ItemId.Of("Desc_Water_C");

    private static FlowNode Node(TestWorld world, string id)
    {
        FlowNetwork network = WorldFlow.From(world.Build(), [Water]);
        return network.Nodes[network.IndexOf[BuildableId.Of(id)]];
    }

    [Fact]
    public void A_machine_asks_and_makes_its_rates_per_minute()
    {
        TestWorld world = new TestWorld().Machine("smelter", Items.IronOre, 30, Items.IronIngot, 30);

        var smelter = Assert.IsType<FlowMachine>(Node(world, "smelter"));

        Assert.Equal(30, smelter.Demand[Items.IronOre]);
        Assert.Equal(30, smelter.Production[Items.IronIngot]);
    }

    [Fact]
    public void A_provider_with_a_known_rate_offers_it()
    {
        TestWorld world = new TestWorld().Miner("miner", Items.IronOre, 120);

        var miner = Assert.IsType<FlowProvider>(Node(world, "miner"));

        Assert.Equal((Items.IronOre, 120.0), (miner.Item, miner.Offer));
    }

    [Fact]
    public void A_provider_whose_rate_isnt_known_supplies_what_is_asked()
    {
        TestWorld world = new TestWorld().Miner("miner");

        Assert.IsType<FlowUnknownSource>(Node(world, "miner"));
    }

    [Fact]
    public void A_belt_carries_at_most_its_capacity()
    {
        TestWorld world = new TestWorld().Belt("belt", capacity: 270);

        Assert.Equal(270, Assert.IsType<FlowConduit>(Node(world, "belt")).Capacity);
    }

    [Fact]
    public void A_belt_whose_capacity_isnt_known_has_no_limit()
    {
        TestWorld world = new TestWorld().Belt("belt");

        Assert.Equal(double.PositiveInfinity, Assert.IsType<FlowConduit>(Node(world, "belt")).Capacity);
    }

    [Fact]
    public void A_consumer_takes_everything()
    {
        TestWorld world = new TestWorld().Sink("sink");

        Assert.IsType<FlowSink>(Node(world, "sink"));
    }

    [Fact]
    public void A_container_at_the_end_of_a_line_takes_everything()
    {
        TestWorld world = new TestWorld().Belt("belt").Container("box").Connect("belt", "box");

        Assert.IsType<FlowSink>(Node(world, "box"));
    }

    private static readonly Inventory Full = Inventory.OfSlots(1, new Dictionary<ItemId, decimal> { [Items.IronOre] = 100 }, new Dictionary<ItemId, int> { [Items.IronOre] = 100 });

    [Fact]
    public void A_container_at_the_end_of_a_line_takes_only_what_fits_in_its_inventory()
    {
        TestWorld world = new TestWorld().Belt("belt").Container("box", Full).Connect("belt", "box");

        Assert.Same(Full, Assert.IsType<FlowSink>(Node(world, "box")).Inventory);
    }

    [Fact]
    public void A_station_that_loads_takes_only_what_fits_in_its_inventory()
    {
        TestWorld world = new TestWorld().Belt("belt").LoadingStation("station", Full).Connect("belt", "station");

        Assert.Same(Full, Assert.IsType<FlowSink>(Node(world, "station")).Inventory);
    }

    // A container with one slot, full of 100 ingots: on its own it takes no more.
    private static readonly Inventory FullOfIngots =
        Inventory.OfSlots(1, new Dictionary<ItemId, decimal> { [Items.IronIngot] = 100 }, new Dictionary<ItemId, int> { [Items.IronIngot] = 100 });

    private static bool TakesIngots(TestWorld world) =>
        Assert.IsType<FlowSink>(Node(world.Belt("belt").Container("box", FullOfIngots).Connect("smelter", "belt", "box"), "box")).Inventory!.Takes(Items.IronIngot);

    [Fact]
    public void A_full_container_still_takes_what_a_running_machine_feeding_it_has_room_to_send()
    {
        // The smelter holds ore and has no ingots waiting to leave: at save time its ingots were leaving.
        Assert.True(TakesIngots(new TestWorld().MachineWaiting("smelter", Items.IronOre, Items.IronIngot, holds: 50, waitingToLeave: 0)));
    }

    [Fact]
    public void A_full_container_takes_nothing_from_a_machine_with_a_full_stack_waiting_to_leave()
    {
        Assert.False(TakesIngots(new TestWorld().MachineWaiting("smelter", Items.IronOre, Items.IronIngot, holds: 50, waitingToLeave: 100)));
    }

    [Fact]
    public void A_machine_whose_next_batch_would_not_fit_shows_nothing_leaving()
    {
        // A batch is 15 ingots: with 90 of 100 waiting, the next one doesn't fit, so the smelter waits.
        Assert.False(TakesIngots(new TestWorld().MachineWaiting("smelter", Items.IronOre, Items.IronIngot, holds: 50, waitingToLeave: 90, batch: 15)));
    }

    [Fact]
    public void A_machine_with_room_for_more_than_its_next_batch_shows_its_products_leaving_even_with_a_backlog()
    {
        Assert.True(TakesIngots(new TestWorld().MachineWaiting("smelter", Items.IronOre, Items.IronIngot, holds: 50, waitingToLeave: 84, batch: 15)));
    }

    [Fact]
    public void A_machine_with_room_for_just_its_next_batch_is_backing_up()
    {
        // A backed-up machine fills its slot, waits until one batch fits again, and fills it: its slot always sits
        // between the stack less a batch and the stack. Your power shard encoder: 99 of 100, a batch of 1.
        Assert.False(TakesIngots(new TestWorld().MachineWaiting("smelter", Items.IronOre, Items.IronIngot, holds: 50, waitingToLeave: 85, batch: 15)));
    }

    [Fact]
    public void A_machine_holding_none_of_an_input_shows_nothing_leaving()
    {
        // An empty output slot of a machine that can't run proves nothing about the container.
        Assert.False(TakesIngots(new TestWorld().MachineWaiting("smelter", Items.IronOre, Items.IronIngot, holds: 0, waitingToLeave: 0)));
    }

    [Fact]
    public void A_full_container_takes_nothing_when_the_save_says_nothing_about_what_waits_to_leave()
    {
        Assert.False(TakesIngots(new TestWorld().MachineWaiting("smelter", Items.IronOre, Items.IronIngot, holds: 50, waitingToLeave: null)));
    }

    [Fact]
    public void A_full_station_still_takes_what_a_running_machine_feeding_it_through_a_splitter_has_room_to_send()
    {
        TestWorld world = new TestWorld().MachineWaiting("smelter", Items.IronOre, Items.IronIngot, holds: 50, waitingToLeave: 3)
            .Belt("in").Splitter("split").Belt("out").LoadingStation("station", FullOfIngots).Connect("smelter", "in", "split", "out", "station");

        Assert.True(Assert.IsType<FlowSink>(Node(world, "station")).Inventory!.Takes(Items.IronIngot));
    }

    [Fact]
    public void What_arrives_at_a_full_container_is_what_the_machines_showing_it_leaving_make()
    {
        // Two smelters of 30 a minute: one with room in its output, one with a full stack waiting (blocked).
        TestWorld world = new TestWorld()
            .MachineWaiting("running", Items.IronOre, Items.IronIngot, holds: 50, waitingToLeave: 0)
            .MachineWaiting("blocked", Items.IronOre, Items.IronIngot, holds: 50, waitingToLeave: 100)
            .Belt("a").Belt("b").Merger("merge").Belt("belt").Container("box", FullOfIngots)
            .Connect("running", "a", "merge", "belt", "box").Connect("blocked", "b", "merge");

        Assert.Equal(30, Assert.IsType<FlowSink>(Node(world, "box")).Inventory!.Arriving[Items.IronIngot]);
    }

    [Fact]
    public void A_machine_whose_line_reaches_several_full_containers_shares_what_it_sends_over_them()
    {
        // One running smelter of 30 a minute, split over two full containers: 15 arrives at each, not 30 at both.
        TestWorld world = new TestWorld()
            .MachineWaiting("smelter", Items.IronOre, Items.IronIngot, holds: 50, waitingToLeave: 0)
            .Belt("in").Splitter("split").Belt("a").Belt("b").Container("box a", FullOfIngots).Container("box b", FullOfIngots)
            .Connect("smelter", "in", "split", "a", "box a").Connect("split", "b", "box b");

        Assert.Equal(15, Assert.IsType<FlowSink>(Node(world, "box a")).Inventory!.Arriving[Items.IronIngot]);
    }

    [Fact]
    public void A_machine_the_save_shows_with_room_for_its_next_batch_has_its_products_leaving()
    {
        TestWorld world = new TestWorld().MachineWaiting("smelter", Items.IronOre, Items.IronIngot, holds: 50, waitingToLeave: 0, batch: 1, stackSize: 100);

        Assert.Equal([Items.IronIngot], Assert.IsType<FlowMachine>(Node(world, "smelter")).Leaving);
    }

    [Fact]
    public void A_machine_whose_next_batch_does_not_fit_has_nothing_leaving()
    {
        TestWorld world = new TestWorld().MachineWaiting("smelter", Items.IronOre, Items.IronIngot, holds: 50, waitingToLeave: 100, batch: 1, stackSize: 100);

        Assert.Empty(Assert.IsType<FlowMachine>(Node(world, "smelter")).Leaving ?? new HashSet<ItemId>());
    }

    [Fact]
    public void A_container_inside_a_line_passes_items_through()
    {
        TestWorld world = new TestWorld().Belt("in").Container("box").Belt("out").Connect("in", "box", "out");

        Assert.Equal(double.PositiveInfinity, Assert.IsType<FlowConduit>(Node(world, "box")).Capacity);
    }

    [Fact]
    public void A_container_nothing_feeds_supplies_what_is_asked()
    {
        TestWorld world = new TestWorld().Container("box").Belt("out").Connect("box", "out");

        Assert.IsType<FlowUnknownSource>(Node(world, "box"));
    }

    // ---- BR-A9: an empty station carries what its neighbours carry ----------------------------------------------

    private static IReadOnlySet<ItemId>? CargoOf(TestWorld world, string station) =>
        Assert.IsType<FlowUnknownSource>(Node(world, station)).Cargo;

    [Fact]
    public void An_empty_station_supplies_what_the_stations_its_lines_meet_hold()
    {
        // Your freight platforms on a load balancer: only one has cargo yet, the empty ones carry the same.
        TestWorld world = new TestWorld().Station("full", (Items.IronOre, 4800)).Station("empty")
            .Belt("a").Belt("b").Merger("merge").Belt("out").Machine("smelter", Items.IronOre, Items.IronIngot)
            .Connect("full", "a", "merge", "out", "smelter").Connect("empty", "b", "merge");

        Assert.Equal([Items.IronOre], CargoOf(world, "empty"));
    }

    [Fact]
    public void An_empty_station_whose_lines_meet_stations_with_different_cargo_supplies_all_of_it()
    {
        TestWorld world = new TestWorld().Station("ore", (Items.IronOre, 4800)).Station("coal", (Items.Coal, 4800)).Station("empty")
            .Belt("a").Belt("b").Belt("c").Merger("merge").Belt("out").Machine("smelter", Items.IronOre, Items.IronIngot)
            .Connect("ore", "a", "merge", "out", "smelter").Connect("coal", "b", "merge").Connect("empty", "c", "merge");

        Assert.Equal(new HashSet<ItemId> { Items.IronOre, Items.Coal }, CargoOf(world, "empty"));
    }

    [Fact]
    public void An_empty_station_whose_lines_meet_no_other_station_supplies_whatever_is_asked()
    {
        TestWorld world = new TestWorld().Station("empty").Belt("out").Machine("smelter", Items.IronOre, Items.IronIngot)
            .Connect("empty", "out", "smelter");

        Assert.Null(CargoOf(world, "empty"));
    }

    [Fact]
    public void A_station_that_unloads_supplies_what_is_asked_and_knows_its_stock()
    {
        TestWorld world = new TestWorld().Station("platform", (Items.IronOre, 4800)).Belt("out").Connect("platform", "out");

        var platform = Assert.IsType<FlowUnknownSource>(Node(world, "platform"));

        Assert.Equal(4800, platform.Stock[Items.IronOre]);
    }

    [Fact]
    public void A_station_that_unloads_knows_what_it_burns()
    {
        TestWorld world = new TestWorld().StationBurning("port", Items.IronPlate);

        Assert.Equal([Items.IronPlate], Assert.IsType<FlowUnknownSource>(Node(world, "port")).Burns!);
    }

    [Fact]
    public void A_station_that_only_loads_knows_what_it_burns()
    {
        TestWorld world = new TestWorld().LoadingStationBurning("truck station", Items.IronPlate);

        Assert.Equal([Items.IronPlate], Assert.IsType<FlowSink>(Node(world, "truck station")).Burns!);
    }

    [Fact]
    public void An_input_whose_stock_lasts_thirty_minutes_counts_as_stocked()
    {
        // 30 a minute for 30 minutes.
        TestWorld world = new TestWorld().MachineHolding("smelter", Items.IronOre, 30, Items.IronIngot, 30, holds: 900);

        Assert.Equal([Items.IronOre], Assert.IsType<FlowMachine>(Node(world, "smelter")).Stocked!);
    }

    [Fact]
    public void An_input_whose_stock_runs_out_sooner_does_not()
    {
        TestWorld world = new TestWorld().MachineHolding("smelter", Items.IronOre, 30, Items.IronIngot, 30, holds: 899);

        Assert.Empty(Assert.IsType<FlowMachine>(Node(world, "smelter")).Stocked!);
    }

    [Fact]
    public void A_generator_whose_fuel_stock_lasts_counts_it_as_stocked()
    {
        // A plutonium plant burns 0.1 rod a minute: 50 rods last 500 minutes.
        TestWorld world = new TestWorld().GeneratorHolding("plant", Items.Coal, 0.1m, holds: 50);

        Assert.Equal([Items.Coal], Assert.IsType<FlowMachine>(Node(world, "plant")).Stocked!);
    }

    [Fact]
    public void A_drone_port_that_unloads_knows_how_much_it_burns()
    {
        TestWorld world = new TestWorld().StationBurning("port", Items.IronPlate, Rate.Of(5, 3));

        Assert.Equal(5.0 / 3, Assert.IsType<FlowUnknownSource>(Node(world, "port")).BurnRate!.Value, 9);
    }

    [Fact]
    public void A_drone_port_that_only_loads_knows_how_much_it_burns()
    {
        TestWorld world = new TestWorld().LoadingStationBurning("port", Items.IronPlate, Rate.PerMinute(2));

        Assert.Equal(2.0, Assert.IsType<FlowSink>(Node(world, "port")).BurnRate);
    }

    [Fact]
    public void A_station_whose_burn_rate_is_not_known_has_none()
    {
        TestWorld world = new TestWorld().StationBurning("port", Items.IronPlate);

        Assert.Null(Assert.IsType<FlowUnknownSource>(Node(world, "port")).BurnRate);
    }

    [Fact]
    public void A_container_nothing_feeds_knows_its_stock()
    {
        TestWorld world = new TestWorld().Container("box", (Items.IronPlate, 751)).Belt("out").Connect("box", "out");

        Assert.Equal(751, Assert.IsType<FlowUnknownSource>(Node(world, "box")).Stock[Items.IronPlate]);
    }

    [Fact]
    public void A_pipe_junction_splits_and_merges_like_a_splitter()
    {
        TestWorld world = new TestWorld().Junction("junction");

        Assert.IsType<FlowSplitter>(Node(world, "junction"));
    }

    [Fact]
    public void A_merger_takes_evenly_from_its_inputs()
    {
        TestWorld world = new TestWorld().Merger("merger");

        Assert.IsType<FlowMerger>(Node(world, "merger"));
    }

    [Fact]
    public void A_belt_carries_solids()
    {
        TestWorld world = new TestWorld().Belt("belt");

        Assert.Equal(Carries.Solids, Assert.IsType<FlowConduit>(Node(world, "belt")).Carries);
    }

    [Fact]
    public void A_pipe_carries_fluids()
    {
        TestWorld world = new TestWorld().Pipe("pipe");

        Assert.Equal(Carries.Fluids, Assert.IsType<FlowConduit>(Node(world, "pipe")).Carries);
    }

    [Fact]
    public void A_container_nothing_feeds_supplies_solids()
    {
        TestWorld world = new TestWorld().Container("box").Belt("out").Connect("box", "out");

        Assert.Equal(Carries.Solids, Assert.IsType<FlowUnknownSource>(Node(world, "box")).Carries);
    }

    [Fact]
    public void A_fluid_buffer_at_the_end_of_a_pipe_takes_everything()
    {
        TestWorld world = new TestWorld().Pipe("pipe").FluidBuffer("tank").Connect("pipe", "tank");

        Assert.IsType<FlowSink>(Node(world, "tank"));
    }

    [Fact]
    public void A_fluid_buffer_inside_a_line_passes_fluids_through()
    {
        TestWorld world = new TestWorld().Pipe("in").FluidBuffer("tank").Pipe("out").Connect("in", "tank", "out");

        Assert.Equal(Carries.Fluids, Assert.IsType<FlowConduit>(Node(world, "tank")).Carries);
    }

    [Fact]
    public void The_network_knows_which_items_are_fluids()
    {
        Assert.Equal([Water], WorldFlow.From(new TestWorld().Build(), [Water]).Fluids);
    }

    [Fact]
    public void A_world_from_a_node_to_a_sink_runs_its_smelter_fully()
    {
        WorldGraph world = new TestWorld()
            .Miner("miner", Items.IronOre, 60).Belt("ore", 60).Machine("smelter", Items.IronOre, 30, Items.IronIngot, 30)
            .Belt("ingots", 60).Sink("sink")
            .Connect("miner", "ore", "smelter", "ingots", "sink")
            .Build();

        FlowResult result = SteadyState.Of(WorldFlow.From(world, fluids: []));

        Assert.Equal(1, result.Machine(BuildableId.Of("smelter")).Utilisation, 0.0001);
    }

    // ---- BR-A8: generators -----------------------------------------------------------------------------------

    private static readonly ItemId UraniumRod = ItemId.Of("Desc_NuclearFuelRod_C");
    private static readonly ItemId UraniumWaste = ItemId.Of("Desc_NuclearWaste_C");

    [Fact]
    public void A_generator_burning_a_known_fuel_asks_for_its_fuel_and_water_and_makes_its_waste()
    {
        TestWorld world = new TestWorld().Generator("plant", UraniumRod, 0.2m, Water, 240, UraniumWaste, 10);

        var plant = Assert.IsType<FlowMachine>(Node(world, "plant"));

        Assert.Equal((0.2, 240.0), (plant.Demand[UraniumRod], plant.Demand[Water]));
        Assert.Equal(10, plant.Production[UraniumWaste]);
    }

    [Fact]
    public void A_generator_burning_a_fuel_without_waste_makes_nothing()
    {
        TestWorld world = new TestWorld().Generator("generator", ItemId.Of("Desc_LiquidFuel_C"), 20);

        Assert.Empty(Assert.IsType<FlowMachine>(Node(world, "generator")).Production);
    }

    [Fact]
    public void A_generator_whose_fuel_isnt_known_takes_everything_like_any_consumer()
    {
        TestWorld world = new TestWorld().Sink("generator");

        Assert.IsType<FlowSink>(Node(world, "generator"));
    }

    [Fact]
    public void An_uploader_takes_only_what_the_lines_other_consumers_leave()
    {
        Assert.True(Assert.IsType<FlowSink>(Node(new TestWorld().Uploader("uploader"), "uploader")).LeftoversOnly);
    }

    [Fact]
    public void A_sink_takes_everything_it_is_offered()
    {
        Assert.False(Assert.IsType<FlowSink>(Node(new TestWorld().Sink("sink"), "sink")).LeftoversOnly);
    }
}
