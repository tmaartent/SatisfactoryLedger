using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Tests;

using static SatisfactoryLedger.World.Domain.Tests.TestFlow;

namespace SatisfactoryLedger.World.Simulation.Tests;

/// <summary>
/// The steady state as a linear programme (ADR-0026), part B: machines, capacities, providers, sinks and loops, without
/// the sharing rules of splitters and mergers yet. The cases are the sweep's (<c>SteadyStateTests</c>).
/// </summary>
public class LinearSteadyStateTests
{
    private const double Precision = 0.0001;

    private static readonly ItemId Ore = Items.IronOre;
    private static readonly ItemId Ingot = Items.IronIngot;
    private static readonly ItemId Plate = Items.IronPlate;
    private static readonly ItemId Coal = Items.Coal;

    private static LinearFlow Solve(TestFlow flow) => LinearSteadyState.Solve(flow.Network());

    // ---- BR-A1: machines ------------------------------------------------------------------------------------

    [Fact]
    public void A_machine_that_gets_all_it_asks_for_runs_at_100_percent()
    {
        LinearFlow result = Solve(new TestFlow()
            .Provider("miner", Ore, 60).Belt("ore").Machine("smelter", Ore, 30, Ingot, 30).Belt("ingots").Sink("sink")
            .Connect("miner", "ore", "smelter", "ingots", "sink"));

        Assert.Equal(1.0, result.Utilisation[Id("smelter")], Precision);
    }

    [Fact]
    public void What_flows_into_a_belt_is_what_its_inputs_bring_together()
    {
        LinearFlow result = Solve(new TestFlow()
            .Provider("miner 1", Ore, 20).Provider("miner 2", Ore, 25).Merger("merge").Belt("out").Sink("sink")
            .Connect("miner 1", "merge", "out", "sink").Connect("miner 2", "merge"));

        Assert.Equal(45, result.Into(Id("out"), Ore), Precision);
    }

    [Fact]
    public void A_machine_that_gets_half_of_its_input_runs_at_half_speed()
    {
        LinearFlow result = Solve(new TestFlow()
            .Provider("miner", Ore, 15).Belt("ore").Machine("smelter", Ore, 30, Ingot, 30).Belt("ingots").Sink("sink")
            .Connect("miner", "ore", "smelter", "ingots", "sink"));

        Assert.Equal(0.5, result.Utilisation[Id("smelter")], Precision);
        Assert.Equal(15, result.Flow(Id("smelter"), Id("ingots"), Ingot), Precision);
    }

    [Fact]
    public void A_machine_whose_output_has_nowhere_to_go_stops()
    {
        LinearFlow result = Solve(new TestFlow()
            .Provider("miner", Ore, 60).Belt("ore").Machine("smelter", Ore, 30, Ingot, 30).Belt("ingots")
            .Connect("miner", "ore", "smelter", "ingots"));

        Assert.Equal(0, result.Utilisation[Id("smelter")], Precision);
    }

    [Fact]
    public void A_machine_with_two_inputs_runs_at_the_share_of_its_scarcest_input()
    {
        LinearFlow result = Solve(new TestFlow()
            .Provider("plates", Plate, 30).Belt("plate belt")
            .Provider("screws", Items.Screw, 30).Belt("screw belt")
            .Machine("assembler", [(Plate, 30), (Items.Screw, 60)], [(Items.ReinforcedIronPlate, 5)]).Belt("out").Sink("sink")
            .Connect("plates", "plate belt", "assembler", "out", "sink")
            .Connect("screws", "screw belt", "assembler"));

        Assert.Equal(0.5, result.Utilisation[Id("assembler")], Precision);
        Assert.Equal(15, result.Flow(Id("plate belt"), Id("assembler"), Plate), Precision);
    }

    // ---- BR-A2: providers, BR-A3: capacity ---------------------------------------------------------------------

    [Fact]
    public void A_provider_delivers_only_what_is_taken()
    {
        LinearFlow result = Solve(new TestFlow()
            .Provider("miner", Ore, 60).Belt("ore").Machine("smelter", Ore, 30, Ingot, 30).Belt("ingots").Sink("sink")
            .Connect("miner", "ore", "smelter", "ingots", "sink"));

        Assert.Equal(30, result.Flow(Id("miner"), Id("ore"), Ore), Precision);
    }

    [Fact]
    public void A_belt_carries_at_most_its_capacity()
    {
        LinearFlow result = Solve(new TestFlow()
            .Provider("miner", Ore, 120).Belt("mk1", capacity: 60).Sink("sink")
            .Connect("miner", "mk1", "sink"));

        Assert.Equal(60, result.Flow(Id("mk1"), Id("sink"), Ore), Precision);
    }

    [Fact]
    public void An_output_belt_too_slow_for_its_machine_holds_the_machine_back()
    {
        LinearFlow result = Solve(new TestFlow()
            .Provider("miner", Ore, 60).Belt("ore").Machine("smelter", Ore, 60, Ingot, 60).Belt("mk1", capacity: 30).Sink("sink")
            .Connect("miner", "ore", "smelter", "mk1", "sink"));

        Assert.Equal(0.5, result.Utilisation[Id("smelter")], Precision);
    }

    // ---- BR-A17: stock ----------------------------------------------------------------------------------------

    [Fact]
    public void A_machine_whose_stock_of_an_input_lasts_runs_while_nothing_brings_that_input()
    {
        LinearFlow result = Solve(new TestFlow()
            .Provider("miner", Ore, 60).Belt("ore")
            .MachineOnStock("foundry", [(Ore, 30), (Coal, 15)], [(Ingot, 30)], Coal).Belt("out").Sink("sink")
            .Connect("miner", "ore", "foundry", "out", "sink"));

        Assert.Equal(1.0, result.Utilisation[Id("foundry")], Precision);
    }

    [Fact]
    public void A_machine_running_on_stock_still_takes_what_its_belt_brings()
    {
        LinearFlow result = Solve(new TestFlow()
            .Provider("miner", Ore, 60).Belt("ore")
            .Provider("coal mine", Coal, 5).Belt("coal")
            .MachineOnStock("foundry", [(Ore, 30), (Coal, 15)], [(Ingot, 30)], Coal).Belt("out").Sink("sink")
            .Connect("miner", "ore", "foundry", "out", "sink")
            .Connect("coal mine", "coal", "foundry"));

        Assert.Equal(5, result.Flow(Id("coal"), Id("foundry"), Coal), Precision);
    }

    // ---- BR-A3: solids over belts, fluids over pipes ---------------------------------------------------------

    private static readonly ItemId CrudeOil = ItemId.Of("Desc_LiquidOil_C");
    private static readonly ItemId Plastic = ItemId.Of("Desc_Plastic_C");
    private static readonly ItemId Residue = ItemId.Of("Desc_HeavyOilResidue_C");

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
        LinearFlow result = Solve(Refinery().Pipe("residue").Sink("residue sink").Connect("refinery", "residue", "residue sink"));

        Assert.Equal((20, 0), (result.Flow(Id("refinery"), Id("plastic"), Plastic), result.Flow(Id("refinery"), Id("plastic"), Residue)));
        Assert.Equal((10, 0), (result.Flow(Id("refinery"), Id("residue"), Residue), result.Flow(Id("refinery"), Id("residue"), Plastic)));
        Assert.Equal(1.0, result.Utilisation[Id("refinery")], Precision);
    }

    [Fact]
    public void A_fluid_product_with_no_pipe_to_leave_by_blocks_the_machine()
    {
        Assert.Equal(0, Solve(Refinery()).Utilisation[Id("refinery")], Precision);
    }

    // ---- BR-A10: loops ---------------------------------------------------------------------------------------

    private static readonly ItemId Water = ItemId.Of("Desc_Water_C");
    private static readonly ItemId Nitrogen = ItemId.Of("Desc_NitrogenGas_C");
    private static readonly ItemId NitricAcid = ItemId.Of("Desc_NitricAcid_C");
    private static readonly ItemId NonFissile = ItemId.Of("Desc_NonFissibleUranium_C");
    private static readonly ItemId Waste = ItemId.Of("Desc_NuclearWaste_C");

    /// <summary>The owner's nitric acid loop: the acid blender needs water that only the non-fissile blender gives back.</summary>
    private static TestFlow NitricAcidLoop(double waterBack) => new TestFlow()
        .Fluid(Water).Fluid(Nitrogen).Fluid(NitricAcid)
        .Provider("nitrogen well", Nitrogen, 10).Provider("waste", Waste, 10)
        .Pipe("nitrogen").Pipe("water").Pipe("acid").Belt("waste belt").Belt("product")
        .Machine("acid blender", [(Nitrogen, 10), (Water, 10)], [(NitricAcid, 10)])
        .Machine("non-fissile blender", [(NitricAcid, 10), (Waste, 10)], [(NonFissile, 10), (Water, waterBack)])
        .Sink("sink")
        .Connect("nitrogen well", "nitrogen", "acid blender", "acid", "non-fissile blender", "water", "acid blender")
        .Connect("waste", "waste belt", "non-fissile blender", "product", "sink");

    [Fact]
    public void A_loop_that_gives_back_all_it_needs_runs_at_100_percent()
    {
        LinearFlow result = Solve(NitricAcidLoop(waterBack: 10));

        Assert.Equal(1.0, result.Utilisation[Id("acid blender")], Precision);
        Assert.Equal(1.0, result.Utilisation[Id("non-fissile blender")], Precision);
    }

    [Fact]
    public void A_loop_that_gives_back_too_little_stops()
    {
        Assert.Equal(0, Solve(NitricAcidLoop(waterBack: 5)).Utilisation[Id("acid blender")], Precision);
    }

    private static readonly ItemId Ficsonium = ItemId.Of("Desc_Ficsonium_C");
    private static readonly ItemId DarkMatterResidue = ItemId.Of("Desc_DarkEnergy_C");
    private static readonly ItemId PlutoniumWaste = ItemId.Of("Desc_PlutoniumWaste_C");
    private static readonly ItemId FicsoniumRod = ItemId.Of("Desc_FicsoniumFuelRod_C");

    [Fact]
    public void An_encoder_and_a_collider_that_feed_each_other_run_at_100_percent()
    {
        // The owner's Ficsonium loop, where the sweeps settle anywhere between 4% and 55% on the big world.
        LinearFlow result = Solve(new TestFlow()
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
            .Connect("encoder", "rods", "plant"));

        Assert.Equal(1.0, result.Utilisation[Id("encoder")], Precision);
        Assert.Equal(1.0, result.Utilisation[Id("collider")], Precision);
    }

    [Fact]
    public void Items_circling_in_a_loop_are_conserved_so_only_what_leaves_the_loop_is_made()
    {
        // The constructor takes 20 of the smelter's 30 ingots; the rest can't leave the loop.
        LinearFlow result = Solve(new TestFlow()
            .Provider("miner", Ore, 30).Machine("smelter", Ore, 30, Ingot, 30)
            .Merger("merge").Belt("loop", capacity: 60).Splitter("split").Belt("back", capacity: 60)
            .Machine("constructor", Ingot, 20, Plate, 20).Belt("out").Sink("sink")
            .Connect("miner", "smelter", "merge", "loop", "split", "constructor", "out", "sink")
            .Connect("split", "back", "merge"));

        Assert.Equal(1.0, result.Utilisation[Id("constructor")], Precision);
        Assert.Equal(2.0 / 3, result.Utilisation[Id("smelter")], Precision);
    }

    // ---- BR-A9: sources of unknown rate -------------------------------------------------------------------------

    private static readonly ItemId Canister = ItemId.Of("Desc_FluidCanister_C");
    private static readonly ItemId PackagedWater = ItemId.Of("Desc_PackagedWater_C");

    [Fact]
    public void A_source_whose_rate_is_not_known_supplies_what_is_asked_and_no_fluid_over_a_belt()
    {
        LinearFlow result = Solve(new TestFlow()
            .Fluid(Water)
            .Provider("extractor", Water, 120).Pipe("water")
            .UnknownSource("container").Belt("canisters")
            .Machine("packager", [(Water, 60), (Canister, 60)], [(PackagedWater, 60)]).Belt("out").Sink("sink")
            .Connect("extractor", "water", "packager", "out", "sink")
            .Connect("container", "canisters", "packager"));

        Assert.Equal((60, 0), (result.Flow(Id("container"), Id("canisters"), Canister), result.Flow(Id("container"), Id("canisters"), Water)));
        Assert.Equal(1.0, result.Utilisation[Id("packager")], Precision);
    }
}
