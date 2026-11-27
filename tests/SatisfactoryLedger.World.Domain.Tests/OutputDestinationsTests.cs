using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Domain.Tests.Tracing;
using SatisfactoryLedger.World.Domain.Tracing;

namespace SatisfactoryLedger.World.Domain.Tests;

/// <summary>
/// Where a factory's output goes (BR-A18, decided 2026-11-11): followed through logistics to the machines, consumers
/// and containers at the ends of its line, shared at each split as the flow there is, and what those receivers would
/// take at 100%.
/// </summary>
public class OutputDestinationsTests
{
    private static BuildableId Id(string name) => BuildableId.Of(name);

    private static Dictionary<string, double> Ends(IReadOnlyDictionary<BuildableId, double> ends) =>
        ends.ToDictionary(end => end.Key.Id, end => Math.Round(end.Value, 4));

    [Fact]
    public void An_output_on_a_plain_belt_all_goes_to_the_machine_at_its_end()
    {
        WorldGraph world = new TestWorld()
            .Belt("plates").Machine("assembler", Items.IronPlate, 30, Items.ReinforcedIronPlate, 5)
            .Connect("plates", "assembler").Build();

        var ends = OutputDestinations.Of(world, Id("plates"), Items.IronPlate, 20, new TestReadings().Into("plates", Items.IronPlate, 20).Build(world));

        Assert.Equal(new Dictionary<string, double> { ["assembler"] = 20 }, Ends(ends));
    }

    [Fact]
    public void A_splitter_shares_it_as_the_flow_on_each_side_is()
    {
        WorldGraph world = new TestWorld()
            .Splitter("splitter").Belt("left").Belt("right")
            .Machine("assembler A", Items.IronPlate, 30, Items.ReinforcedIronPlate, 5)
            .Machine("assembler B", Items.IronPlate, 30, Items.ReinforcedIronPlate, 5)
            .Connect("splitter", "left", "assembler A").Connect("splitter", "right", "assembler B").Build();

        var ends = OutputDestinations.Of(
            world, Id("splitter"), Items.IronPlate, 30, new TestReadings().Into("left", Items.IronPlate, 20).Into("right", Items.IronPlate, 10).Build(world));

        Assert.Equal(new Dictionary<string, double> { ["assembler A"] = 20, ["assembler B"] = 10 }, Ends(ends));
    }

    [Fact]
    public void Where_other_supply_merges_in_it_goes_on_in_proportion_to_its_share_of_the_mix()
    {
        // 10 of ours and 30 from elsewhere merge; the 40 split evenly, so each side gets a quarter of ours on each.
        WorldGraph world = new TestWorld()
            .Belt("ours").Belt("theirs").Merger("merger").Belt("mixed").Splitter("splitter").Belt("left").Belt("right")
            .Machine("assembler A", Items.IronPlate, 30, Items.ReinforcedIronPlate, 5)
            .Machine("assembler B", Items.IronPlate, 30, Items.ReinforcedIronPlate, 5)
            .Connect("ours", "merger", "mixed", "splitter", "left", "assembler A")
            .Connect("theirs", "merger").Connect("splitter", "right", "assembler B").Build();

        FlowReadings readings = new TestReadings()
            .Into("ours", Items.IronPlate, 10).Into("theirs", Items.IronPlate, 30).Into("mixed", Items.IronPlate, 40)
            .Into("left", Items.IronPlate, 20).Into("right", Items.IronPlate, 20).Build(world);

        var ends = OutputDestinations.Of(world, Id("ours"), Items.IronPlate, 10, readings);

        Assert.Equal(new Dictionary<string, double> { ["assembler A"] = 5, ["assembler B"] = 5 }, Ends(ends));
    }

    [Fact]
    public void A_line_into_a_container_ends_there()
    {
        WorldGraph world = new TestWorld().Belt("plates").Container("box").Connect("plates", "box").Build();

        var ends = OutputDestinations.Of(
            world, Id("plates"), Items.IronPlate, 20, new TestReadings().Into("plates", Items.IronPlate, 20).Into("box", Items.IronPlate, 20).Build(world));

        Assert.Equal(new Dictionary<string, double> { ["box"] = 20 }, Ends(ends));
    }

    [Fact]
    public void A_belt_that_goes_nowhere_is_its_own_end()
    {
        WorldGraph world = new TestWorld().Belt("plates").Connect("plates").Build();

        var ends = OutputDestinations.Of(world, Id("plates"), Items.IronPlate, 20, new TestReadings().Into("plates", Items.IronPlate, 20).Build(world));

        Assert.Equal(new Dictionary<string, double> { ["plates"] = 20 }, Ends(ends));
    }

    [Fact]
    public void A_line_that_loops_back_is_followed_once()
    {
        // The splitter sends half back round to the merger before it: that half isn't followed a second time.
        WorldGraph world = new TestWorld()
            .Belt("plates").Merger("merger").Belt("on").Splitter("splitter").Belt("back").Belt("out").Sink("sink")
            .Connect("plates", "merger", "on", "splitter", "out", "sink").Connect("splitter", "back", "merger").Build();

        var ends = OutputDestinations.Of(world, Id("plates"), Items.IronPlate, 20, new TestReadings()
            .Into("plates", Items.IronPlate, 20).Into("on", Items.IronPlate, 20).Into("out", Items.IronPlate, 20).Build(world));

        Assert.Equal(new Dictionary<string, double> { ["sink"] = 20 }, Ends(ends));
    }

    [Fact]
    public void Its_receivers_would_take_what_their_recipes_ask_for_at_100_percent_whatever_else_feeds_them()
    {
        WorldGraph world = new TestWorld()
            .Splitter("splitter").Belt("left").Belt("right")
            .Machine("assembler A", Items.IronPlate, 30, Items.ReinforcedIronPlate, 5)
            .Machine("assembler B", Items.IronPlate, 15, Items.ReinforcedIronPlate, 2.5m)
            .Connect("splitter", "left", "assembler A").Connect("splitter", "right", "assembler B").Build();

        Assert.Equal(Rate.PerMinute(45), OutputDestinations.ReceiversNeed(world, [Id("splitter")], Items.IronPlate));
    }

    [Fact]
    public void A_receiver_that_does_not_use_the_item_asks_for_none_of_it()
    {
        WorldGraph world = new TestWorld()
            .Belt("mixed").Machine("constructor", Items.IronIngot, 30, Items.IronRod, 15)
            .Connect("mixed", "constructor").Build();

        Assert.Equal(Rate.Zero, OutputDestinations.ReceiversNeed(world, [Id("mixed")], Items.IronPlate));
    }

    [Fact]
    public void A_sink_or_a_container_asks_for_nothing_in_particular()
    {
        WorldGraph world = new TestWorld()
            .Splitter("splitter").Belt("left").Belt("right").Sink("sink").Container("box")
            .Connect("splitter", "left", "sink").Connect("splitter", "right", "box").Build();

        Assert.Equal(Rate.Zero, OutputDestinations.ReceiversNeed(world, [Id("splitter")], Items.IronPlate));
    }

    [Fact]
    public void A_generator_asks_for_its_fuel_and_its_water()
    {
        WorldGraph world = new TestWorld()
            .Belt("rods").Pipe("water").Generator("plant", Items.UraniumFuelRod, 0.2m, Items.Water, 240, Items.UraniumWaste, 10)
            .Connect("rods", "plant").Connect("water", "plant").Build();

        Assert.Equal(
            (Rate.PerMinute(0.2m), Rate.PerMinute(240)),
            (OutputDestinations.ReceiversNeed(world, [Id("rods")], Items.UraniumFuelRod), OutputDestinations.ReceiversNeed(world, [Id("water")], Items.Water)));
    }

    [Fact]
    public void A_receiver_is_counted_once_however_many_lines_lead_to_it()
    {
        WorldGraph world = new TestWorld()
            .Belt("left").Belt("right").Machine("assembler", Items.IronPlate, 30, Items.ReinforcedIronPlate, 5)
            .Connect("left", "assembler").Connect("right", "assembler").Build();

        Assert.Equal(Rate.PerMinute(30), OutputDestinations.ReceiversNeed(world, [Id("left"), Id("right")], Items.IronPlate));
    }

    [Fact]
    public void Its_receivers_get_what_flows_into_them_now_from_any_source_each_once()
    {
        // Two lines lead to the assembler, which runs at 80% of its 30 a minute; a sink takes the rest. Only the assembler
        // takes plates for a recipe.
        WorldGraph world = new TestWorld()
            .Belt("left").Belt("right").Machine("assembler", Items.IronPlate, 30, Items.ReinforcedIronPlate, 5)
            .Belt("rest").Sink("sink")
            .Connect("left", "assembler").Connect("right", "assembler").Connect("right", "rest", "sink").Build();
        FlowReadings readings = new TestReadings().Starved("assembler", Items.IronPlate, 0.8).Into("rest", Items.IronPlate, 6).Build(world);

        Assert.Equal(24, OutputDestinations.ReceiversGet(world, [Id("left"), Id("right")], Items.IronPlate, readings), 4);
    }

    [Fact]
    public void A_drone_port_asks_for_the_fuel_its_drones_burn()
    {
        WorldGraph world = new TestWorld()
            .Belt("batteries").LoadingStationBurning("drone port", Items.Battery, Rate.PerMinute(2))
            .Connect("batteries", "drone port").Build();

        Assert.Equal(Rate.PerMinute(2), OutputDestinations.ReceiversNeed(world, [Id("batteries")], Items.Battery));
    }
}
