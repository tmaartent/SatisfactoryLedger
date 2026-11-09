using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.Domain.Flow.Objects;

using static SatisfactoryLedger.World.Domain.Tests.TestFlow;

namespace SatisfactoryLedger.World.Domain.Tests;

/// <summary>The steady-state flow simulation (BR-A1 to BR-A10), on small hand-built networks.</summary>
public class SteadyStateTests
{
    private const double Precision = 0.0001;

    private static readonly ItemId Ore = Items.IronOre;
    private static readonly ItemId Ingot = Items.IronIngot;
    private static readonly ItemId Plate = Items.IronPlate;
    private static readonly ItemId Fuel = ItemId.Of("Desc_LiquidFuel_C");
    private static readonly ItemId Rods = ItemId.Of("Desc_NuclearFuelRod_C");
    private static readonly ItemId Waste = ItemId.Of("Desc_NuclearWaste_C");

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
    public void What_flows_into_a_belt_is_what_its_inputs_bring_together()
    {
        FlowResult result = new TestFlow()
            .Provider("miner 1", Ore, 20).Provider("miner 2", Ore, 25).Merger("merge").Belt("out").Sink("sink")
            .Connect("miner 1", "merge", "out", "sink").Connect("miner 2", "merge")
            .Run();

        Assert.Equal(45, result.Into(Id("out"))[Ore], Precision);
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

    [Fact]
    public void A_machine_held_back_by_less_than_a_tenth_of_a_percent_is_running()
    {
        // 10,000 ingots a minute onto a belt that takes 9,995: 99.95%.
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 10_000).Machine("smelter", Ore, 10_000, Ingot, 10_000).Belt("belt", capacity: 9_995).Sink("sink")
            .Connect("miner", "smelter", "belt", "sink")
            .Run();

        MachineState smelter = result.Machine(Id("smelter"));
        Assert.Equal(MachineLimit.None, smelter.Limit);
        Assert.Null(smelter.LimitingItem);
    }

    [Fact]
    public void A_machine_held_back_by_a_fifth_of_a_percent_is_still_output_blocked()
    {
        // 10,000 ingots a minute onto a belt that takes 9,980: 99.8%.
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 10_000).Machine("smelter", Ore, 10_000, Ingot, 10_000).Belt("belt", capacity: 9_980).Sink("sink")
            .Connect("miner", "smelter", "belt", "sink")
            .Run();

        Assert.Equal(MachineLimit.OutputBlocked, result.Machine(Id("smelter")).Limit);
    }

    // ---- BR-A8: generators -----------------------------------------------------------------------------------

    [Fact]
    public void A_generator_takes_only_the_fuel_it_burns_and_leaves_the_rest_on_the_belt()
    {
        // A fuel generator burns 20 m³ a minute; 50 are offered.
        FlowResult result = new TestFlow()
            .Fluid(Fuel).Provider("refinery", Fuel, 50).Pipe("pipe").Machine("generator", [(Fuel, 20)], [])
            .Connect("refinery", "pipe", "generator")
            .Run();

        Assert.Equal(20, result.Into(Id("generator"))[Fuel], Precision);
        Assert.Equal(MachineLimit.None, result.Machine(Id("generator")).Limit);
    }

    [Fact]
    public void A_nuclear_plant_whose_waste_cannot_leave_stops()
    {
        FlowResult result = new TestFlow()
            .Provider("rods", Rods, 1).Belt("in").Machine("plant", Rods, 0.2, Waste, 10).Belt("waste")
            .Connect("rods", "in", "plant", "waste")
            .Run();

        MachineState plant = result.Machine(Id("plant"));
        Assert.Equal((0.0, MachineLimit.OutputBlocked, Waste), (plant.Utilisation, plant.Limit, plant.LimitingItem));
    }

    [Fact]
    public void An_uploader_behind_a_smart_splitter_gets_only_what_the_factory_leaves()
    {
        // As in the owner's world: "any" to the uploader, "overflow" to the factory. A full depot takes nothing more.
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 300).Belt("in").SmartSplitter("split", Rule(2, SortRuleKind.Any), Rule(0, SortRuleKind.Overflow))
            .Belt("to factory").Machine("cells", [(Ore, 100)], []).Belt("to depot").Uploader("uploader")
            .Connect("miner", "in", "split").ConnectPort("split", 0, "to factory").Connect("to factory", "cells")
            .ConnectPort("split", 2, "to depot").Connect("to depot", "uploader")
            .Run();

        Assert.Equal(1.0, result.Machine(Id("cells")).Utilisation, Precision);
        Assert.Equal(200, result.Flow(Id("to depot"), Id("uploader"), Ore), Precision);
    }

    [Fact]
    public void What_an_uploader_takes_from_the_leftovers_is_still_offered_to_the_factory()
    {
        // The owner's uranium line: the machine takes only half its ore, because only half its coal comes. The uploader
        // gets the rest, but the machine is starved of coal, not of ore: all the ore is still offered to it.
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 60).Belt("ore").SmartSplitter("split", new SortRule(0, SortRuleKind.Overflow), new SortRule(2, SortRuleKind.Any))
            .Belt("to factory").Machine("foundry", [(Ore, 60), (Coal, 30)], []).Belt("to depot").Uploader("uploader")
            .Provider("coal mine", Coal, 15).Belt("coal")
            .Connect("miner", "ore", "split").ConnectPort("split", 0, "to factory").Connect("to factory", "foundry")
            .ConnectPort("split", 2, "to depot").Connect("to depot", "uploader")
            .Connect("coal mine", "coal", "foundry")
            .Run();

        Assert.Equal((0.5, MachineLimit.Starved, (ItemId?)Coal), (result.Machine(Id("foundry")).Utilisation, result.Machine(Id("foundry")).Limit, result.Machine(Id("foundry")).LimitingItem));
        Assert.Equal(30, result.Flow(Id("to depot"), Id("uploader"), Ore), Precision);
    }

    [Fact]
    public void What_an_uploader_takes_behind_a_plain_splitter_is_still_offered_to_the_factory()
    {
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 60).Belt("ore").Splitter("split")
            .Belt("to factory").Machine("foundry", [(Ore, 60), (Coal, 30)], []).Belt("to depot").Uploader("uploader")
            .Provider("coal mine", Coal, 15).Belt("coal")
            .Connect("miner", "ore", "split", "to factory", "foundry")
            .Connect("split", "to depot", "uploader")
            .Connect("coal mine", "coal", "foundry")
            .Run();

        Assert.Equal((MachineLimit.Starved, (ItemId?)Coal), (result.Machine(Id("foundry")).Limit, result.Machine(Id("foundry")).LimitingItem));
    }

    [Fact]
    public void An_uploader_at_the_end_of_a_line_takes_everything_that_reaches_it()
    {
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 60).Belt("in").Uploader("uploader")
            .Connect("miner", "in", "uploader")
            .Run();

        Assert.Equal(60, result.Flow(Id("in"), Id("uploader"), Ore), Precision);
        Assert.Empty(result.UnusedSupply);
    }

    // ---- BR-A10: loops -------------------------------------------------------------------------------------------

    private static readonly ItemId Nitrogen = ItemId.Of("Desc_NitrogenGas_C");
    private static readonly ItemId NitricAcid = ItemId.Of("Desc_NitricAcid_C");
    private static readonly ItemId NonFissile = ItemId.Of("Desc_NonFissibleUranium_C");

    /// <summary>
    /// The owner's nitric acid loop: the acid blender needs water that only the non-fissile blender gives back, and
    /// that one needs the acid. Both at zero is a steady state too. Whether a loop that can keep itself going should be
    /// found running is open: solving from the top was tried in step 23 and taken out (see its log).
    /// </summary>
    private static FlowResult NitricAcidLoop(double waterBack) => new TestFlow()
        .Fluid(Water).Fluid(Nitrogen).Fluid(NitricAcid)
        .Provider("nitrogen well", Nitrogen, 10).Provider("waste", Waste, 10)
        .Pipe("nitrogen").Pipe("water").Pipe("acid").Belt("waste belt").Belt("product")
        .Machine("acid blender", [(Nitrogen, 10), (Water, 10)], [(NitricAcid, 10)])
        .Machine("non-fissile blender", [(NitricAcid, 10), (Waste, 10)], [(NonFissile, 10), (Water, waterBack)])
        .Sink("sink")
        .Connect("nitrogen well", "nitrogen", "acid blender", "acid", "non-fissile blender", "water", "acid blender")
        .Connect("waste", "waste belt", "non-fissile blender", "product", "sink")
        .Run();

    [Fact]
    public void A_loop_that_gives_back_all_it_needs_runs_as_it_does_once_started_in_the_game()
    {
        FlowResult result = NitricAcidLoop(waterBack: 10);

        Assert.Equal(1.0, result.Machine(Id("acid blender")).Utilisation, Precision);
        Assert.Equal(1.0, result.Machine(Id("non-fissile blender")).Utilisation, Precision);
    }

    private static readonly ItemId Ficsonium = ItemId.Of("Desc_Ficsonium_C");
    private static readonly ItemId DarkMatterResidue = ItemId.Of("Desc_DarkEnergy_C");
    private static readonly ItemId PlutoniumWaste = ItemId.Of("Desc_PlutoniumWaste_C");
    private static readonly ItemId FicsoniumRod = ItemId.Of("Desc_FicsoniumFuelRod_C");

    [Fact]
    public void An_encoder_and_a_collider_that_feed_each_other_run_as_they_do_in_the_game()
    {
        // The owner's Ficsonium loop: the collider needs the residue the encoder leaves, half of it; a converter makes
        // the other half. The encoder needs the collider's Ficsonium. The residue meets in one pipe network.
        FlowResult result = new TestFlow()
            .Fluid(DarkMatterResidue)
            .Provider("converter", DarkMatterResidue, 60).Pipe("converter pipe")
            .Provider("waste", PlutoniumWaste, 6).Belt("waste belt")
            .Machine("encoder", [(Ficsonium, 6)], [(FicsoniumRod, 3), (DarkMatterResidue, 60)]).Pipe("encoder pipe")
            .PipeNetwork("residue network")
            .Pipe("collider pipe")
            .Machine("collider", [(DarkMatterResidue, 120), (PlutoniumWaste, 6)], [(Ficsonium, 6)]).Belt("ficsonium")
            .Belt("rods").Sink("plant")
            .Connect("converter", "converter pipe", "residue network", "collider pipe", "collider", "ficsonium", "encoder", "encoder pipe", "residue network")
            .Connect("waste", "waste belt", "collider")
            .Connect("encoder", "rods", "plant")
            .Run();

        Assert.Equal(1.0, result.Machine(Id("encoder")).Utilisation, Precision);
        Assert.Equal(1.0, result.Machine(Id("collider")).Utilisation, Precision);
    }

    [Fact]
    public void A_loop_that_gives_back_too_little_winds_down()
    {
        // Half the water comes back each round: 50%, 25%, … down to nothing.
        FlowResult result = NitricAcidLoop(waterBack: 5);

        Assert.Equal(0, result.Machine(Id("acid blender")).Utilisation, 0.001);
        Assert.Equal(MachineLimit.Starved, result.Machine(Id("acid blender")).Limit);
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
    public void A_machine_with_each_input_from_its_own_station_is_supplied_fully()
    {
        // Each station offers what its belt can carry, without waiting for the machine to accept the other input first.
        FlowResult result = new TestFlow()
            .UnknownSource("casing station").Belt("casings", capacity: 120)
            .UnknownSource("frame station").Belt("frames", capacity: 120)
            .Machine("blender", [(Ore, 30), (Plate, 20)], [(Ingot, 30)]).Belt("out").Sink("sink")
            .Connect("casing station", "casings", "blender", "out", "sink")
            .Connect("frame station", "frames", "blender")
            .Run();

        Assert.True(result.Settled);
        Assert.Equal(1.0, result.Machine(Id("blender")).Utilisation, Precision);
    }

    [Fact]
    public void Two_belts_bringing_the_same_item_to_a_machine_share_what_it_asks()
    {
        // Each belt offers 30 and the smelter asks 30: it takes 15 from each, as from a merger, and that settles.
        FlowResult result = new TestFlow()
            .Provider("miner 1", Ore, 30).Belt("ore 1").Provider("miner 2", Ore, 30).Belt("ore 2")
            .Machine("smelter", Ore, 30, Ingot, 30).Belt("ingots").Sink("sink")
            .Connect("miner 1", "ore 1", "smelter", "ingots", "sink")
            .Connect("miner 2", "ore 2", "smelter")
            .Run();

        Assert.True(result.Settled);
        Assert.Equal(15, result.Flow(Id("ore 1"), Id("smelter"), Ore), Precision);
        Assert.Equal(15, result.Flow(Id("ore 2"), Id("smelter"), Ore), Precision);
    }

    [Fact]
    public void The_largest_part_of_your_big_world_settles()
    {
        // 15,328 buildables of "Desert Start Cleaned" (step 15). Manufacturers that take motors and cooling systems
        // from manifolds flip-flop between two states for good unless what connections accept is damped. No smaller
        // piece of it does: every neighbourhood of up to 7,880 nodes settles on its own.
        FlowNetwork part = FlowText.ReadGzip("Fixtures/big-world-part.flow.gz");

        FlowResult result = SteadyState.Of(part);

        Assert.True(result.Settled);
        Assert.Equal(15328, part.Nodes.Count);
    }

    [Fact]
    public void A_source_whose_rate_is_not_known_offers_what_its_belt_can_carry()
    {
        FlowResult result = new TestFlow()
            .UnknownSource("station").Belt("ore", capacity: 120).Machine("smelter", Ore, 30, Ingot, 30).Belt("ingots").Sink("sink")
            .Connect("station", "ore", "smelter", "ingots", "sink")
            .Run();

        Assert.Equal(30, result.Flow(Id("station"), Id("ore"), Ore), Precision);
        Assert.Empty(result.Bottlenecks);
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

    [Fact]
    public void A_drone_port_takes_everything_its_input_brings_and_supplies_its_output()
    {
        // It sends what comes in to another port, and unloads what drones bring: both at once.
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 120).Belt("to port")
            .UnknownSource("drone port").Belt("from port").Machine("smelter", Ore, 30, Ingot, 30).Belt("ingots").Sink("sink")
            .Connect("miner", "to port", "drone port", "from port", "smelter", "ingots", "sink")
            .Run();

        Assert.Empty(result.UnusedSupply);
        Assert.Equal(30, result.Flow(Id("drone port"), Id("from port"), Ore), Precision);
    }

    // ---- BR-A4: smart splitters ----------------------------------------------------------------------------------

    private static SortRule Rule(int output, SortRuleKind kind, ItemId? item = null) => new(output, kind, item);

    /// <summary>60 ore into a smart splitter: output 0 to a smelter that burns 40, output 1 to a sink.</summary>
    private static FlowResult SplitOre(SortRuleKind second) => new TestFlow()
        .Provider("miner", Ore, 60).Belt("in").SmartSplitter("split", Rule(0, SortRuleKind.Any), Rule(1, second))
        .Belt("main").Machine("smelter", [(Ore, 40)], []).Belt("second").Sink("sink")
        .Connect("miner", "in", "split").ConnectPort("split", 0, "main").Connect("main", "smelter")
        .ConnectPort("split", 1, "second").Connect("second", "sink")
        .Run();

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
        FlowResult result = SplitOre(SortRuleKind.Any);

        Assert.Equal(30, result.Flow(Id("second"), Id("sink"), Ore), Precision);
    }

    /// <summary>A belt with 30 ore and 20 plates a minute into a smart splitter, to a smelter and a constructor.</summary>
    private static FlowResult SortMixed(SortRule second) => new TestFlow()
        .Provider("miner", Ore, 30).Provider("plates", Plate, 20).Merger("merge").Belt("mixed")
        .SmartSplitter("sort", Rule(0, SortRuleKind.Item, Ore), second)
        .Belt("ore").Machine("smelter", Ore, 30, Ingot, 30).Belt("ingots").Sink("ingot sink")
        .Belt("plate").Machine("constructor", [(Plate, 20)], []).Sink("plate sink")
        .Connect("miner", "merge", "mixed", "sort").Connect("plates", "merge")
        .ConnectPort("sort", 0, "ore").Connect("ore", "smelter", "ingots", "ingot sink")
        .ConnectPort("sort", 1, "plate").Connect("plate", "constructor")
        .Run();

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
    private static FlowResult Merge(double first, double second, int firstPriority = 2) => new TestFlow()
        .Provider("first", Ore, first).Belt("first line").Provider("second", Ore, second).Belt("second line")
        .PriorityMerger("merge", firstPriority, 0).Belt("out", capacity: 60).Sink("sink")
        .Connect("first", "first line").ConnectInto("first line", "merge", 0)
        .Connect("second", "second line").ConnectInto("second line", "merge", 1)
        .Connect("merge", "out", "sink")
        .Run();

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
        FlowResult result = Merge(40, 60);

        Assert.Equal(20, result.Flow(Id("second line"), Id("merge"), Ore), Precision);
    }

    [Fact]
    public void Inputs_of_equal_priority_share_evenly_like_a_plain_merger()
    {
        FlowResult result = Merge(60, 60, firstPriority: 0);

        Assert.Equal(30, result.Flow(Id("second line"), Id("merge"), Ore), Precision);
    }

    // ---- Which inputs are short, which outputs can't leave (BR-A11) ------------------------------------------------

    [Fact]
    public void A_machine_short_of_two_inputs_names_both_with_the_share_it_is_offered_of_each()
    {
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 15).Belt("ore").Provider("coal mine", Coal, 12).Belt("coal")
            .Machine("foundry", [(Ore, 30), (Coal, 15)], [(Ingot, 30)]).Belt("out").Sink("sink")
            .Connect("miner", "ore", "foundry", "out", "sink")
            .Connect("coal mine", "coal", "foundry")
            .Run();

        MachineState foundry = result.Machine(Id("foundry"));
        Assert.Equal(0.5, foundry.StarvedOf![Ore], Precision);
        Assert.Equal(0.8, foundry.StarvedOf[Coal], Precision);
    }

    [Fact]
    public void A_machine_short_of_one_input_names_only_that_one()
    {
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 15).Belt("ore").Provider("coal mine", Coal, 60).Belt("coal")
            .Machine("foundry", [(Ore, 30), (Coal, 15)], [(Ingot, 30)]).Belt("out").Sink("sink")
            .Connect("miner", "ore", "foundry", "out", "sink")
            .Connect("coal mine", "coal", "foundry")
            .Run();

        Assert.Equal([Ore], result.Machine(Id("foundry")).StarvedOf!.Keys);
    }

    [Fact]
    public void A_machine_whose_output_cannot_leave_names_the_product_with_the_share_that_can()
    {
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 60).Belt("ore").Machine("smelter", Ore, 30, Ingot, 30).Belt("out", capacity: 15).Sink("sink")
            .Connect("miner", "ore", "smelter", "out", "sink")
            .Run();

        MachineState smelter = result.Machine(Id("smelter"));
        Assert.Equal(0.5, smelter.BlockedOn![Ingot], Precision);
        Assert.Empty(smelter.StarvedOf!);
    }

    [Fact]
    public void A_running_machine_names_nothing()
    {
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 60).Belt("ore").Machine("smelter", Ore, 30, Ingot, 30).Belt("out").Sink("sink")
            .Connect("miner", "ore", "smelter", "out", "sink")
            .Run();

        Assert.Equal((0, 0), (result.Machine(Id("smelter")).StarvedOf!.Count, result.Machine(Id("smelter")).BlockedOn!.Count));
    }

    // ---- Running on stock (BR-A17): what a machine holds counts as supplied when it lasts --------------------------

    private static readonly ItemId Coal = Items.Coal;

    [Fact]
    public void A_machine_whose_stock_of_an_input_lasts_runs_while_nothing_brings_that_input()
    {
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 60).Belt("ore")
            .MachineOnStock("foundry", [(Ore, 30), (Coal, 15)], [(Ingot, 30)], Coal).Belt("out").Sink("sink")
            .Connect("miner", "ore", "foundry", "out", "sink")
            .Run();

        Assert.Equal((1.0, MachineLimit.None), (result.Machine(Id("foundry")).Utilisation, result.Machine(Id("foundry")).Limit));
    }

    [Fact]
    public void A_machine_running_on_stock_still_takes_what_its_belt_brings()
    {
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 60).Belt("ore")
            .Provider("coal mine", Coal, 5).Belt("coal")
            .MachineOnStock("foundry", [(Ore, 30), (Coal, 15)], [(Ingot, 30)], Coal).Belt("out").Sink("sink")
            .Connect("miner", "ore", "foundry", "out", "sink")
            .Connect("coal mine", "coal", "foundry")
            .Run();

        Assert.Equal(5, result.Flow(Id("coal"), Id("foundry"), Coal), Precision);
    }

    [Fact]
    public void What_a_machine_running_on_stock_makes_flows_on()
    {
        FlowResult result = new TestFlow()
            .MachineOnStock("smelter", [(Ore, 30)], [(Ingot, 30)], Ore).Belt("out").Sink("sink")
            .Connect("smelter", "out", "sink")
            .Run();

        Assert.Equal(30, result.Flow(Id("out"), Id("sink"), Ingot), Precision);
    }

    [Fact]
    public void A_machine_without_stock_is_still_starved_of_what_nothing_brings()
    {
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 60).Belt("ore")
            .Machine("foundry", [(Ore, 30), (Coal, 15)], [(Ingot, 30)]).Belt("out").Sink("sink")
            .Connect("miner", "ore", "foundry", "out", "sink")
            .Run();

        Assert.Equal((MachineLimit.Starved, (ItemId?)Coal), (result.Machine(Id("foundry")).Limit, result.Machine(Id("foundry")).LimitingItem));
    }

    // ---- Fuel for drones and trucks: what the other consumers leave ------------------------------------------------

    private static readonly ItemId RocketFuel = ItemId.Of("Desc_PackagedRocketFuel_C");

    /// <summary>60 rocket fuel split between a machine that burns 40 and a station that burns it too.</summary>
    private static TestFlow FuelLine(Func<TestFlow, TestFlow> station) => station(new TestFlow()
            .Provider("fuel source", RocketFuel, 60).Belt("fuel").Splitter("split")
            .Belt("to generator").Machine("generator", [(RocketFuel, 40)], [])
            .Belt("to station"))
        .Connect("fuel source", "fuel", "split", "to generator", "generator")
        .Connect("split", "to station", "station");

    [Fact]
    public void A_drone_port_takes_only_the_fuel_the_other_consumers_on_the_line_leave()
    {
        FlowResult result = FuelLine(flow => flow.StationBurning("station", RocketFuel)).Run();

        Assert.Equal(1.0, result.Machine(Id("generator")).Utilisation, Precision);
        Assert.Equal(20, result.Flow(Id("to station"), Id("station"), RocketFuel), Precision);
    }

    [Fact]
    public void A_truck_station_that_only_loads_takes_only_the_fuel_the_others_leave()
    {
        FlowResult result = FuelLine(flow => flow.LoadingStationBurning("station", RocketFuel)).Run();

        Assert.Equal(1.0, result.Machine(Id("generator")).Utilisation, Precision);
    }

    [Fact]
    public void A_drone_port_that_burns_a_known_rate_takes_no_more_fuel_than_that()
    {
        FlowResult result = FuelLine(flow => flow.StationBurning("station", RocketFuel, 10)).Run();

        Assert.Equal(10, result.Flow(Id("to station"), Id("station"), RocketFuel), Precision);
    }

    [Fact]
    public void A_drone_port_that_burns_a_known_rate_shares_the_line_with_the_other_consumers()
    {
        // 40 for the machine and 30 for the port is more than the 60 there is: the splitter divides it evenly.
        FlowResult result = FuelLine(flow => flow.StationBurning("station", RocketFuel, 30)).Run();

        Assert.Equal(0.75, result.Machine(Id("generator")).Utilisation, Precision);
        Assert.Equal(30, result.Flow(Id("to station"), Id("station"), RocketFuel), Precision);
    }

    [Fact]
    public void A_drone_port_that_only_loads_takes_no_more_fuel_than_it_burns()
    {
        FlowResult result = FuelLine(flow => flow.LoadingStationBurning("station", RocketFuel, 10)).Run();

        Assert.Equal(10, result.Flow(Id("to station"), Id("station"), RocketFuel), Precision);
    }

    [Fact]
    public void Fuel_over_two_belts_into_a_drone_port_together_makes_up_what_it_burns()
    {
        FlowResult result = new TestFlow()
            .Provider("first source", RocketFuel, 60).Belt("first")
            .Provider("second source", RocketFuel, 60).Belt("second")
            .StationBurning("drone port", RocketFuel, 10)
            .Connect("first source", "first", "drone port")
            .Connect("second source", "second", "drone port")
            .Run();

        Assert.Equal(10, result.Flow(Id("first"), Id("drone port"), RocketFuel) + result.Flow(Id("second"), Id("drone port"), RocketFuel), Precision);
    }

    [Fact]
    public void A_source_whose_rate_is_not_known_sends_a_drone_port_the_fuel_it_burns()
    {
        FlowResult result = new TestFlow()
            .UnknownSource("container").Belt("fuel").StationBurning("drone port", RocketFuel, 10)
            .Connect("container", "fuel", "drone port")
            .Run();

        Assert.Equal(10, result.Flow(Id("fuel"), Id("drone port"), RocketFuel), Precision);
    }

    [Fact]
    public void A_source_whose_rate_is_not_known_does_not_ask_for_what_a_station_burns_at_an_unknown_rate()
    {
        FlowResult result = new TestFlow()
            .UnknownSource("container").Belt("fuel").StationBurning("drone port", RocketFuel)
            .Connect("container", "fuel", "drone port")
            .Run();

        Assert.Equal(0, result.Flow(Id("fuel"), Id("drone port"), RocketFuel), Precision);
    }

    [Fact]
    public void Cargo_for_a_station_is_taken_like_by_any_consumer()
    {
        // Ore isn't what it burns: the splitter divides it evenly, so the machine gets 30 of its 40.
        FlowResult result = new TestFlow()
            .Provider("miner", Ore, 60).Belt("ore").Splitter("split")
            .Belt("to smelter").Machine("smelter", [(Ore, 40)], [])
            .Belt("to station").StationBurning("station", RocketFuel)
            .Connect("miner", "ore", "split", "to smelter", "smelter")
            .Connect("split", "to station", "station")
            .Run();

        Assert.Equal(0.75, result.Machine(Id("smelter")).Utilisation, Precision);
    }

    [Fact]
    public void A_machine_whose_fuel_only_goes_to_a_drone_port_still_runs()
    {
        FlowResult result = new TestFlow()
            .Provider("source", Ore, 60).Belt("in").Machine("packager", Ore, 60, RocketFuel, 60).Belt("out")
            .StationBurning("drone port", RocketFuel)
            .Connect("source", "in", "packager", "out", "drone port")
            .Run();

        Assert.Equal(1.0, result.Machine(Id("packager")).Utilisation, Precision);
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
