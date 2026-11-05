using SatisfactoryLedger.World.Domain.Flow;
using static SatisfactoryLedger.World.Domain.Tests.TestFlow;

namespace SatisfactoryLedger.World.Domain.Tests;

/// <summary>The steady-state flow simulation (BR-A1 to BR-A10), on small hand-built networks.</summary>
public class SteadyStateTests
{
    private const double Precision = 0.0001;

    private static readonly ItemId Ore = Items.IronOre;
    private static readonly ItemId Ingot = Items.IronIngot;
    private static readonly ItemId Plate = Items.IronPlate;

    // ---- BR-A1: machines ------------------------------------------------------------------------------------

    [Fact]
    public void A_machine_that_gets_all_it_asks_for_runs_at_100_percent()
    {
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 60).Belt("ore").Machine("smelter", Ore, 30, Ingot, 30).Belt("ingots").Sink("sink")
            .Connect("miner", "ore", "smelter", "ingots", "sink")
            .Run();

        Assert.Equal(1.0, result.Machine(Id("smelter")).Utilisation, Precision);
        Assert.Equal(MachineLimit.None, result.Machine(Id("smelter")).Limit);
    }

    [Fact]
    public void A_machine_that_gets_half_of_its_input_runs_at_half_speed_and_is_starved_of_that_item()
    {
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 15).Belt("ore").Machine("smelter", Ore, 30, Ingot, 30).Belt("ingots").Sink("sink")
            .Connect("miner", "ore", "smelter", "ingots", "sink")
            .Run();

        MachineState smelter = result.Machine(Id("smelter"));
        Assert.Equal(0.5, smelter.Utilisation, Precision);
        Assert.Equal(MachineLimit.Starved, smelter.Limit);
        Assert.Equal(Ore, smelter.LimitingItem);
        Assert.Equal(15, result.Flow(Id("smelter"), Id("ingots"), Ingot), Precision);
    }

    [Fact]
    public void A_machine_whose_output_has_nowhere_to_go_stops_and_is_output_blocked()
    {
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 60).Belt("ore").Machine("smelter", Ore, 30, Ingot, 30).Belt("ingots")
            .Connect("miner", "ore", "smelter", "ingots")
            .Run();

        MachineState smelter = result.Machine(Id("smelter"));
        Assert.Equal(0, smelter.Utilisation, Precision);
        Assert.Equal(MachineLimit.OutputBlocked, smelter.Limit);
        Assert.Equal(Ingot, smelter.LimitingItem);
    }

    [Fact]
    public void A_machine_with_two_inputs_runs_at_the_share_of_its_scarcest_input()
    {
        ItemId screw = Items.Screw;
        FlowResult result = new TestFlow()
            .Provider("plates", Plate, 30).Belt("plate belt")
            .Provider("screws", screw, 30).Belt("screw belt")
            .Machine("assembler", [(Plate, 30), (screw, 60)], [(Items.ReinforcedIronPlate, 5)]).Belt("out").Sink("sink")
            .Connect("plates", "plate belt", "assembler", "out", "sink")
            .Connect("screws", "screw belt", "assembler")
            .Run();

        MachineState assembler = result.Machine(Id("assembler"));
        Assert.Equal(0.5, assembler.Utilisation, Precision);
        Assert.Equal(screw, assembler.LimitingItem);
        Assert.Equal(15, result.Flow(Id("plate belt"), Id("assembler"), Plate), Precision);
    }

    // ---- BR-A2: providers -----------------------------------------------------------------------------------

    [Fact]
    public void A_provider_delivers_only_what_is_taken_and_the_rest_is_unused_supply()
    {
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 60).Belt("ore").Machine("smelter", Ore, 30, Ingot, 30).Belt("ingots").Sink("sink")
            .Connect("miner", "ore", "smelter", "ingots", "sink")
            .Run();

        UnusedSupply unused = Assert.Single(result.UnusedSupply);
        Assert.Equal(Id("miner"), unused.Provider);
        Assert.Equal(60, unused.Offered, Precision);
        Assert.Equal(30, unused.Delivered, Precision);
    }

    // ---- BR-A3: belt capacity, BR-A12: bottlenecks -----------------------------------------------------------

    [Fact]
    public void A_belt_carries_at_most_its_capacity()
    {
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 120).Belt("mk1", capacity: 60).Sink("sink")
            .Connect("miner", "mk1", "sink")
            .Run();

        Assert.Equal(60, result.Flow(Id("mk1"), Id("sink"), Ore), Precision);
    }

    [Fact]
    public void A_belt_at_capacity_with_more_offered_upstream_is_a_bottleneck()
    {
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 120).Belt("mk1", capacity: 60).Sink("sink")
            .Connect("miner", "mk1", "sink")
            .Run();

        Bottleneck bottleneck = Assert.Single(result.Bottlenecks);
        Assert.Equal(Id("mk1"), bottleneck.Conduit);
        Assert.Equal(60, bottleneck.OfferedMore, Precision);
    }

    [Fact]
    public void A_belt_below_its_capacity_is_not_a_bottleneck()
    {
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 30).Belt("mk1", capacity: 60).Sink("sink")
            .Connect("miner", "mk1", "sink")
            .Run();

        Assert.Empty(result.Bottlenecks);
    }

    [Fact]
    public void A_line_of_equally_slow_segments_is_one_bottleneck_at_its_first_segment()
    {
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 120).Belt("first", capacity: 60).Belt("second", capacity: 60).Belt("third", capacity: 60).Sink("sink")
            .Connect("miner", "first", "second", "third", "sink")
            .Run();

        Assert.Equal([Id("first")], result.Bottlenecks.Select(bottleneck => bottleneck.Conduit));
    }

    [Fact]
    public void An_output_belt_too_slow_for_its_machine_is_a_bottleneck_and_the_machine_is_output_blocked()
    {
        // A smelter making 120 ingots a minute onto a Mk.1 belt (60).
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 120).Machine("smelter", Ore, 120, Ingot, 120).Belt("mk1", capacity: 60).Sink("sink")
            .Connect("miner", "smelter", "mk1", "sink")
            .Run();

        Assert.Equal(60, Assert.Single(result.Bottlenecks).OfferedMore, Precision);
        Assert.Equal(0.5, result.Machine(Id("smelter")).Utilisation, Precision);
        Assert.Equal(MachineLimit.OutputBlocked, result.Machine(Id("smelter")).Limit);
    }

    // ---- BR-A4: splitters -------------------------------------------------------------------------------------

    [Fact]
    public void A_splitter_divides_evenly_when_every_output_can_take_its_share()
    {
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 60).Splitter("split")
            .Machine("left", Ore, 40, Ingot, 40).Machine("right", Ore, 40, Ingot, 40).Sink("sink")
            .Connect("miner", "split", "left", "sink")
            .Connect("split", "right", "sink")
            .Run();

        Assert.Equal(0.75, result.Machine(Id("left")).Utilisation, Precision);
        Assert.Equal(0.75, result.Machine(Id("right")).Utilisation, Precision);
    }

    [Fact]
    public void What_one_splitter_output_cannot_take_goes_to_the_others()
    {
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 60).Splitter("split")
            .Machine("small", Ore, 20, Ingot, 20).Machine("big", Ore, 40, Ingot, 40).Sink("sink")
            .Connect("miner", "split", "small", "sink")
            .Connect("split", "big", "sink")
            .Run();

        Assert.Equal(20, result.Flow(Id("split"), Id("small"), Ore), Precision);
        Assert.Equal(40, result.Flow(Id("split"), Id("big"), Ore), Precision);
    }

    [Fact]
    public void A_manifold_short_of_supply_feeds_the_first_machines_more_than_the_last()
    {
        // 60 ore for three machines that want 30 each (the BR-S3 example): 30, 15, 15, not 20 each.
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 60).Splitter("s1").Splitter("s2")
            .Machine("m1", Ore, 30, Ingot, 30).Machine("m2", Ore, 30, Ingot, 30).Machine("m3", Ore, 30, Ingot, 30).Sink("sink")
            .Connect("miner", "s1", "m1", "sink")
            .Connect("s1", "s2", "m2", "sink")
            .Connect("s2", "m3", "sink")
            .Run();

        Assert.Equal(1.0, result.Machine(Id("m1")).Utilisation, Precision);
        Assert.Equal(0.5, result.Machine(Id("m2")).Utilisation, Precision);
        Assert.Equal(0.5, result.Machine(Id("m3")).Utilisation, Precision);
    }

    // ---- BR-A5: mergers ---------------------------------------------------------------------------------------

    [Fact]
    public void A_merger_over_capacity_takes_evenly_from_its_inputs()
    {
        FlowResult result = new TestFlow()
            .Provider("a", Ore, 40).Provider("b", Ore, 40).Merger("merge").Belt("mk1", capacity: 60).Sink("sink")
            .Connect("a", "merge", "mk1", "sink")
            .Connect("b", "merge")
            .Run();

        Assert.Equal(30, result.Flow(Id("a"), Id("merge"), Ore), Precision);
        Assert.Equal(30, result.Flow(Id("b"), Id("merge"), Ore), Precision);
    }

    [Fact]
    public void A_merger_input_that_offers_less_leaves_its_share_to_the_others()
    {
        FlowResult result = new TestFlow()
            .Provider("a", Ore, 10).Provider("b", Ore, 60).Merger("merge").Belt("mk1", capacity: 60).Sink("sink")
            .Connect("a", "merge", "mk1", "sink")
            .Connect("b", "merge")
            .Run();

        Assert.Equal(10, result.Flow(Id("a"), Id("merge"), Ore), Precision);
        Assert.Equal(50, result.Flow(Id("b"), Id("merge"), Ore), Precision);
    }

    // ---- BR-A7 and BR-A8: containers and consumers ------------------------------------------------------------

    [Fact]
    public void A_sink_takes_everything_it_is_offered()
    {
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 480).Belt("belt").Sink("sink")
            .Connect("miner", "belt", "sink")
            .Run();

        Assert.Equal(480, result.Flow(Id("belt"), Id("sink"), Ore), Precision);
        Assert.Empty(result.UnusedSupply);
    }

    // ---- BR-A9: sources with an unknown rate ------------------------------------------------------------------

    [Fact]
    public void A_source_with_an_unknown_rate_supplies_what_is_asked_and_what_it_feeds_is_assumed_supplied()
    {
        FlowResult result = new TestFlow()
            .UnknownSource("station").Belt("ore").Machine("smelter", Ore, 30, Ingot, 30).Belt("ingots").Sink("sink")
            .Connect("station", "ore", "smelter", "ingots", "sink")
            .Run();

        Assert.Equal(1.0, result.Machine(Id("smelter")).Utilisation, Precision);
        UnknownSupply supply = Assert.Single(result.UnknownSupply);
        Assert.Equal(Id("station"), supply.Source);
        Assert.False(supply.Confirmed);
    }

    [Fact]
    public void A_source_with_stock_for_30_minutes_or_more_is_confirmed_supplied()
    {
        // The packager's canisters from the test world: 60 a minute asked, 1,800 in stock, exactly 30 minutes.
        ItemId canister = ItemId.Of("Desc_FluidCanister_C");
        FlowResult result = new TestFlow()
            .UnknownSource("container", (canister, 1800)).Belt("canisters")
            .Machine("packager", canister, 60, ItemId.Of("Desc_PackagedWater_C"), 60).Belt("out").Sink("sink")
            .Connect("container", "canisters", "packager", "out", "sink")
            .Run();

        UnknownSupply supply = Assert.Single(result.UnknownSupply);
        Assert.True(supply.Confirmed);
        Assert.Equal(30, supply.StockMinutes!.Value, Precision);
    }

    [Fact]
    public void A_source_whose_stock_runs_out_sooner_is_assumed_supplied_with_how_long_it_lasts()
    {
        ItemId canister = ItemId.Of("Desc_FluidCanister_C");
        FlowResult result = new TestFlow()
            .UnknownSource("container", (canister, 751)).Belt("canisters")
            .Machine("packager", canister, 60, ItemId.Of("Desc_PackagedWater_C"), 60).Belt("out").Sink("sink")
            .Connect("container", "canisters", "packager", "out", "sink")
            .Run();

        UnknownSupply supply = Assert.Single(result.UnknownSupply);
        Assert.False(supply.Confirmed);
        Assert.Equal(751.0 / 60, supply.StockMinutes!.Value, Precision);
    }

    // ---- Belts carry solids, pipes carry fluids ----------------------------------------------------------------

    private static readonly ItemId Water = ItemId.Of("Desc_Water_C");
    private static readonly ItemId Canister = ItemId.Of("Desc_FluidCanister_C");
    private static readonly ItemId PackagedWater = ItemId.Of("Desc_PackagedWater_C");
    private static readonly ItemId CrudeOil = ItemId.Of("Desc_LiquidOil_C");
    private static readonly ItemId Plastic = ItemId.Of("Desc_Plastic_C");
    private static readonly ItemId Residue = ItemId.Of("Desc_HeavyOilResidue_C");

    /// <summary>The packager of the test world: water over a pipe from an extractor, canisters over a belt from a container.</summary>
    private static TestFlow Packager() => new TestFlow()
        .Fluid(Water)
        .Provider("extractor", Water, 120).Pipe("water")
        .UnknownSource("container").Belt("canisters")
        .Machine("packager", [(Water, 60), (Canister, 60)], [(PackagedWater, 60)]).Belt("out").Sink("sink")
        .Connect("extractor", "water", "packager", "out", "sink")
        .Connect("container", "canisters", "packager");

    [Fact]
    public void A_source_whose_rate_is_not_known_sends_no_fluid_over_a_belt()
    {
        FlowResult result = Packager().Run();

        Assert.Equal(0, result.Flow(Id("container"), Id("canisters"), Water), Precision);
        Assert.Equal(60, result.Flow(Id("container"), Id("canisters"), Canister), Precision);
    }

    [Fact]
    public void A_machine_takes_its_fluid_from_the_pipe()
    {
        FlowResult result = Packager().Run();

        Assert.Equal(60, result.Flow(Id("water"), Id("packager"), Water), Precision);
        Assert.Equal(1.0, result.Machine(Id("packager")).Utilisation, Precision);
    }

    /// <summary>A plastic refinery: 30 oil makes 20 plastic and 10 residue.</summary>
    private static TestFlow Refinery() => new TestFlow()
        .Fluid(CrudeOil).Fluid(Residue)
        .Provider("pump", CrudeOil, 30).Pipe("oil")
        .Machine("refinery", [(CrudeOil, 30)], [(Plastic, 20), (Residue, 10)])
        .Belt("plastic").Sink("plastic sink")
        .Connect("pump", "oil", "refinery", "plastic", "plastic sink");

    [Fact]
    public void A_machine_sends_each_product_over_the_line_that_carries_its_form()
    {
        FlowResult result = Refinery()
            .Pipe("residue").Sink("residue sink").Connect("refinery", "residue", "residue sink")
            .Run();

        Assert.Equal((20, 0), (result.Flow(Id("refinery"), Id("plastic"), Plastic), result.Flow(Id("refinery"), Id("plastic"), Residue)));
        Assert.Equal((10, 0), (result.Flow(Id("refinery"), Id("residue"), Residue), result.Flow(Id("refinery"), Id("residue"), Plastic)));
        Assert.Equal(1.0, result.Machine(Id("refinery")).Utilisation, Precision);
    }

    [Fact]
    public void A_fluid_product_with_no_pipe_to_leave_by_blocks_the_machine()
    {
        FlowResult result = Refinery().Run();

        MachineState refinery = result.Machine(Id("refinery"));
        Assert.Equal(0, refinery.Utilisation, Precision);
        Assert.Equal(MachineLimit.OutputBlocked, refinery.Limit);
        Assert.Equal(Residue, refinery.LimitingItem);
    }

    // ---- Settling ----------------------------------------------------------------------------------------------

    [Fact]
    public void A_branch_that_rejoins_the_line_settles_in_one_sweep_and_two_that_confirm_it()
    {
        // Each node is handled after everything that feeds it, so the merger waits for the long branch.
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 60).Splitter("split").Belt("short")
            .Belt("long 1").Belt("long 2").Belt("long 3").Merger("merge").Belt("out").Sink("sink")
            .Connect("miner", "split", "short", "merge", "out", "sink")
            .Connect("split", "long 1", "long 2", "long 3", "merge")
            .Run();

        Assert.Equal(3, result.Sweeps);
    }

    /// <summary>Two lines that don't touch: a smelter line, and the loop of the conservation test below.</summary>
    private static TestFlow TwoParts() => new TestFlow()
        .Provider("miner", Ore, 60).Belt("ore").Machine("smelter", Ore, 30, Ingot, 30).Belt("ingots").Sink("sink")
        .Connect("miner", "ore", "smelter", "ingots", "sink")
        .Provider("loop miner", Ore, 30).Machine("loop smelter", Ore, 30, Ingot, 30)
        .Merger("merge").Belt("loop", capacity: 60).Splitter("split").Belt("back", capacity: 60)
        .Machine("constructor", Ingot, 20, Plate, 20).Belt("out").Sink("loop sink")
        .Connect("loop miner", "loop smelter", "merge", "loop", "split", "constructor", "out", "loop sink")
        .Connect("split", "back", "merge");

    [Fact]
    public void Parts_of_the_world_that_do_not_touch_are_settled_on_their_own()
    {
        FlowResult result = TwoParts().Run();

        Assert.Equal(2, result.Parts.Count);
        Assert.All(result.Parts, part => Assert.True(part.Settled));
        Assert.Equal(3, result.PartOf(Id("smelter")).Sweeps);
    }

    [Fact]
    public void A_part_that_does_not_settle_within_the_limit_is_approximate_while_the_others_still_settle()
    {
        FlowResult result = TwoParts().Run(maxSweeps: 4);

        Assert.True(result.PartOf(Id("smelter")).Settled);
        Assert.False(result.PartOf(Id("loop smelter")).Settled);
        Assert.False(result.Settled);
    }

    [Fact]
    public void A_part_has_settled_when_no_flow_changes_by_more_than_the_tolerance_for_two_sweeps()
    {
        // The loop comes closer to its steady state by a fraction each sweep. It stops at BR-A10's 0.0001 a minute
        // after 21 sweeps; waiting until changes are below 1e-9 took 38.
        FlowResult result = TwoParts().Run();

        Assert.InRange(result.PartOf(Id("loop smelter")).Sweeps, 3, 25);
    }

    // ---- Loops and BR-A10 -------------------------------------------------------------------------------------

    [Fact]
    public void Items_circling_in_a_loop_are_conserved_so_the_source_delivers_only_what_leaves_the_loop()
    {
        // smelter → merge → loop → split → constructor; split → back → merge. The constructor takes 20 of the 30
        // ingots, the rest can't leave the loop, so in a steady state the smelter runs at 20/30 and is output blocked.
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 30).Machine("smelter", Ore, 30, Ingot, 30)
            .Merger("merge").Belt("loop", capacity: 60).Splitter("split").Belt("back", capacity: 60)
            .Machine("constructor", Ingot, 20, Plate, 20).Belt("out").Sink("sink")
            .Connect("miner", "smelter", "merge", "loop", "split", "constructor", "out", "sink")
            .Connect("split", "back", "merge")
            .Run();

        Assert.True(result.Settled);
        Assert.Equal(1.0, result.Machine(Id("constructor")).Utilisation, Precision);
        Assert.Equal(2.0 / 3, result.Machine(Id("smelter")).Utilisation, Precision);
        Assert.Equal(MachineLimit.OutputBlocked, result.Machine(Id("smelter")).Limit);
    }
}
