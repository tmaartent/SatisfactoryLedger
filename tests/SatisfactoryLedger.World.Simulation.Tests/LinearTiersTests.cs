using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow.Objects;
using SatisfactoryLedger.World.Domain.Tests;

using static SatisfactoryLedger.World.Domain.Tests.TestFlow;

namespace SatisfactoryLedger.World.Simulation.Tests;

/// <summary>
/// What one group gets only after another can take no more (ADR-0026, part C2): smart splitter rules (BR-A4),
/// priority mergers (BR-A5), uploaders and the fuel of stations that take only leftovers (BR-A8), and drone ports that
/// burn a known rate (BR-A16). The cases are the sweep's.
/// </summary>
public class LinearTiersTests
{
    private const double Precision = 0.0001;

    private static readonly ItemId Ore = Items.IronOre;
    private static readonly ItemId Ingot = Items.IronIngot;
    private static readonly ItemId Plate = Items.IronPlate;
    private static readonly ItemId Coal = Items.Coal;
    private static readonly ItemId RocketFuel = ItemId.Of("Desc_PackagedRocketFuel_C");

    private static FlowResult Solve(TestFlow flow) => LinearSteadyState.Of(flow.Network());

    private static SortRule Rule(int output, SortRuleKind kind, ItemId? item = null) => new(output, kind, item);

    // ---- BR-A4: smart splitters ----------------------------------------------------------------------------------

    /// <summary>60 ore into a smart splitter: output 0 to a smelter that burns 40, output 1 to a sink.</summary>
    private static FlowResult SplitOre(SortRuleKind second) => Solve(new TestFlow()
        .Provider("miner", Ore, 60).Belt("in").SmartSplitter("split", Rule(0, SortRuleKind.Any), Rule(1, second))
        .Belt("main").Machine("smelter", [(Ore, 40)], []).Belt("second").Sink("sink")
        .Connect("miner", "in", "split").ConnectPort("split", 0, "main").Connect("main", "smelter")
        .ConnectPort("split", 1, "second").Connect("second", "sink"));

    [Fact]
    public void An_overflow_output_gets_only_what_the_other_outputs_cannot_take()
    {
        FlowResult result = SplitOre(SortRuleKind.Overflow);

        Assert.Equal(1.0, result.Machine(Id("smelter")).Utilisation, Precision);
        Assert.Equal(20, result.Flow(Id("second"), Id("sink"), Ore), Precision);
    }

    [Fact]
    public void An_output_set_to_none_gets_nothing()
    {
        FlowResult result = SplitOre(SortRuleKind.None);

        Assert.Equal(0, result.Flow(Id("second"), Id("sink"), Ore), Precision);
        Assert.Equal(1.0, result.Machine(Id("smelter")).Utilisation, Precision);
    }

    [Fact]
    public void Two_outputs_set_to_any_share_evenly_like_a_plain_splitter()
    {
        Assert.Equal(30, SplitOre(SortRuleKind.Any).Flow(Id("second"), Id("sink"), Ore), Precision);
    }

    /// <summary>A belt with 30 ore and 20 plates a minute into a smart splitter, to a smelter and a constructor.</summary>
    private static FlowResult SortMixed(SortRule second) => Solve(new TestFlow()
        .Provider("miner", Ore, 30).Provider("plates", Plate, 20).Merger("merge").Belt("mixed")
        .SmartSplitter("sort", Rule(0, SortRuleKind.Item, Ore), second)
        .Belt("ore").Machine("smelter", Ore, 30, Ingot, 30).Belt("ingots").Sink("ingot sink")
        .Belt("plate").Machine("constructor", [(Plate, 20)], []).Sink("plate sink")
        .Connect("miner", "merge", "mixed", "sort").Connect("plates", "merge")
        .ConnectPort("sort", 0, "ore").Connect("ore", "smelter", "ingots", "ingot sink")
        .ConnectPort("sort", 1, "plate").Connect("plate", "constructor"));

    [Fact]
    public void An_item_rule_sorts_a_mixed_belt_so_each_machine_gets_its_item()
    {
        FlowResult result = SortMixed(Rule(1, SortRuleKind.Item, Plate));

        Assert.Equal(1.0, result.Machine(Id("smelter")).Utilisation, Precision);
        Assert.Equal(1.0, result.Machine(Id("constructor")).Utilisation, Precision);
    }

    [Fact]
    public void Any_undefined_takes_the_items_no_other_output_names()
    {
        FlowResult result = SortMixed(Rule(1, SortRuleKind.AnyUndefined));

        Assert.Equal(0, result.Flow(Id("sort"), Id("plate"), Ore), Precision);
        Assert.Equal(1.0, result.Machine(Id("constructor")).Utilisation, Precision);
    }

    // ---- BR-A5: priority mergers ---------------------------------------------------------------------------------

    /// <summary>Two lines into a priority merger, onto a belt that carries 60: input 0 has priority 2, input 1 has 0.</summary>
    private static FlowResult Merge(double first, double second, int firstPriority = 2) => Solve(new TestFlow()
        .Provider("first", Ore, first).Belt("first line").Provider("second", Ore, second).Belt("second line")
        .PriorityMerger("merge", firstPriority, 0).Belt("out", capacity: 60).Sink("sink")
        .Connect("first", "first line").ConnectInto("first line", "merge", 0)
        .Connect("second", "second line").ConnectInto("second line", "merge", 1)
        .Connect("merge", "out", "sink"));

    [Fact]
    public void A_priority_merger_takes_from_its_highest_priority_input_first()
    {
        FlowResult result = Merge(60, 60);

        Assert.Equal(60, result.Flow(Id("first line"), Id("merge"), Ore), Precision);
        Assert.Equal(0, result.Flow(Id("second line"), Id("merge"), Ore), Precision);
    }

    [Fact]
    public void A_priority_merger_tops_up_from_a_lower_priority_input()
    {
        Assert.Equal(20, Merge(40, 60).Flow(Id("second line"), Id("merge"), Ore), Precision);
    }

    [Fact]
    public void Inputs_of_equal_priority_share_evenly_like_a_plain_merger()
    {
        Assert.Equal(30, Merge(60, 60, firstPriority: 0).Flow(Id("second line"), Id("merge"), Ore), Precision);
    }

    // ---- BR-A8: uploaders take what the factory leaves -----------------------------------------------------------

    [Fact]
    public void An_uploader_behind_a_smart_splitter_gets_only_what_the_factory_leaves()
    {
        // As in the owner's world: "any" to the uploader, "overflow" to the factory. The overflow side is served first.
        FlowResult result = Solve(new TestFlow()
            .Provider("miner", Ore, 300).Belt("in").SmartSplitter("split", Rule(2, SortRuleKind.Any), Rule(0, SortRuleKind.Overflow))
            .Belt("to factory").Machine("cells", [(Ore, 100)], []).Belt("to depot").Uploader("uploader")
            .Connect("miner", "in", "split").ConnectPort("split", 0, "to factory").Connect("to factory", "cells")
            .ConnectPort("split", 2, "to depot").Connect("to depot", "uploader"));

        Assert.Equal(1.0, result.Machine(Id("cells")).Utilisation, Precision);
        Assert.Equal(200, result.Flow(Id("to depot"), Id("uploader"), Ore), Precision);
    }

    [Fact]
    public void An_uploader_behind_a_plain_splitter_gets_what_a_machine_short_of_another_input_leaves()
    {
        // The foundry gets only half its coal, so it takes half its ore; the uploader takes the rest.
        FlowResult result = Solve(new TestFlow()
            .Provider("miner", Ore, 60).Belt("ore").Splitter("split")
            .Belt("to factory").Machine("foundry", [(Ore, 60), (Coal, 30)], []).Belt("to depot").Uploader("uploader")
            .Provider("coal mine", Coal, 15).Belt("coal")
            .Connect("miner", "ore", "split", "to factory", "foundry")
            .Connect("split", "to depot", "uploader")
            .Connect("coal mine", "coal", "foundry"));

        Assert.Equal(0.5, result.Machine(Id("foundry")).Utilisation, Precision);
        Assert.Equal(30, result.Flow(Id("to depot"), Id("uploader"), Ore), Precision);
    }

    [Fact]
    public void An_uploader_at_the_end_of_a_line_takes_everything_that_reaches_it()
    {
        FlowResult result = Solve(new TestFlow()
            .Provider("miner", Ore, 60).Belt("in").Uploader("uploader")
            .Connect("miner", "in", "uploader"));

        Assert.Equal(60, result.Flow(Id("in"), Id("uploader"), Ore), Precision);
    }

    // ---- BR-A8, BR-A16: fuel for drones and trucks ----------------------------------------------------------------

    /// <summary>60 rocket fuel split between a machine that burns 40 and a station that burns it too.</summary>
    private static FlowResult FuelLine(Func<TestFlow, TestFlow> station) => Solve(station(new TestFlow()
            .Provider("fuel source", RocketFuel, 60).Belt("fuel").Splitter("split")
            .Belt("to generator").Machine("generator", [(RocketFuel, 40)], [])
            .Belt("to station"))
        .Connect("fuel source", "fuel", "split", "to generator", "generator")
        .Connect("split", "to station", "station"));

    [Fact]
    public void A_drone_port_takes_only_the_fuel_the_other_consumers_on_the_line_leave()
    {
        FlowResult result = FuelLine(flow => flow.StationBurning("station", RocketFuel));

        Assert.Equal(1.0, result.Machine(Id("generator")).Utilisation, Precision);
        Assert.Equal(20, result.Flow(Id("to station"), Id("station"), RocketFuel), Precision);
    }

    [Fact]
    public void A_truck_station_that_only_loads_takes_only_the_fuel_the_others_leave()
    {
        Assert.Equal(1.0, FuelLine(flow => flow.LoadingStationBurning("station", RocketFuel)).Machine(Id("generator")).Utilisation, Precision);
    }

    [Fact]
    public void A_drone_port_that_burns_a_known_rate_takes_no_more_fuel_than_that()
    {
        Assert.Equal(10, FuelLine(flow => flow.StationBurning("station", RocketFuel, 10)).Flow(Id("to station"), Id("station"), RocketFuel), Precision);
    }

    [Fact]
    public void A_drone_port_that_burns_a_known_rate_shares_the_line_with_the_other_consumers()
    {
        // 40 for the machine and 30 for the port is more than the 60 there is: the splitter divides it evenly.
        FlowResult result = FuelLine(flow => flow.StationBurning("station", RocketFuel, 30));

        Assert.Equal(0.75, result.Machine(Id("generator")).Utilisation, Precision);
        Assert.Equal(30, result.Flow(Id("to station"), Id("station"), RocketFuel), Precision);
    }

    [Fact]
    public void A_drone_port_that_only_loads_takes_no_more_fuel_than_it_burns()
    {
        Assert.Equal(10, FuelLine(flow => flow.LoadingStationBurning("station", RocketFuel, 10)).Flow(Id("to station"), Id("station"), RocketFuel), Precision);
    }

    [Fact]
    public void A_source_whose_rate_is_not_known_sends_a_drone_port_the_fuel_it_burns()
    {
        FlowResult result = Solve(new TestFlow()
            .UnknownSource("container").Belt("fuel").StationBurning("drone port", RocketFuel, 10)
            .Connect("container", "fuel", "drone port"));

        Assert.Equal(10, result.Flow(Id("fuel"), Id("drone port"), RocketFuel), Precision);
    }

    [Fact]
    public void A_source_whose_rate_is_not_known_does_not_ask_for_what_a_station_burns_at_an_unknown_rate()
    {
        FlowResult result = Solve(new TestFlow()
            .UnknownSource("container").Belt("fuel").StationBurning("drone port", RocketFuel)
            .Connect("container", "fuel", "drone port"));

        Assert.Equal(0, result.Flow(Id("fuel"), Id("drone port"), RocketFuel), Precision);
    }

    [Fact]
    public void Cargo_for_a_station_is_taken_like_by_any_consumer()
    {
        // Ore isn't what it burns: the splitter divides it evenly, so the machine gets 30 of its 40.
        FlowResult result = Solve(new TestFlow()
            .Provider("miner", Ore, 60).Belt("ore").Splitter("split")
            .Belt("to smelter").Machine("smelter", [(Ore, 40)], [])
            .Belt("to station").StationBurning("station", RocketFuel)
            .Connect("miner", "ore", "split", "to smelter", "smelter")
            .Connect("split", "to station", "station"));

        Assert.Equal(0.75, result.Machine(Id("smelter")).Utilisation, Precision);
    }
}
