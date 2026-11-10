using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.Domain.Flow.Objects;
using SatisfactoryLedger.World.Domain.Tests;

using static SatisfactoryLedger.World.Domain.Tests.TestFlow;

namespace SatisfactoryLedger.World.Simulation.Tests;

/// <summary>
/// The running start (ADR-0026): the most that can flow, from a linear programme, and the sweeps started from it. Loops
/// and regions that wait on each other start running; what can't keep going winds down.
/// </summary>
public class RunningStartTests
{
    private const double Precision = 0.0001;

    private static readonly ItemId Ore = Items.IronOre;
    private static readonly ItemId Ingot = Items.IronIngot;

    private static FlowResult Sweep(TestFlow flow)
    {
        FlowNetwork network = flow.Network();
        return SteadyState.Of(network, start: RunningStart.Of(network));
    }

    [Fact]
    public void The_running_start_of_a_line_is_the_most_that_can_flow_through_it()
    {
        FlowNetwork network = new TestFlow()
            .Provider("miner", Ore, 60).Belt("ore").Machine("smelter", Ore, 30, Ingot, 30).Belt("ingots").Sink("sink")
            .Connect("miner", "ore", "smelter", "ingots", "sink")
            .Network();

        IReadOnlyList<IReadOnlyDictionary<ItemId, double>> start = RunningStart.Of(network);

        Assert.Equal(30, start[0][Ore], Precision);
        Assert.Equal(30, start[3][Ingot], Precision);
    }

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
    public void A_loop_that_gives_back_all_it_needs_keeps_running_at_100_percent()
    {
        Assert.Equal(1.0, Sweep(NitricAcidLoop(waterBack: 10)).Machine(Id("acid blender")).Utilisation, Precision);
    }

    [Fact]
    public void A_loop_that_gives_back_too_little_winds_down()
    {
        Assert.Equal(0, Sweep(NitricAcidLoop(waterBack: 5)).Machine(Id("acid blender")).Utilisation, 0.001);
    }

    private static readonly ItemId Ficsonium = ItemId.Of("Desc_Ficsonium_C");
    private static readonly ItemId DarkMatterResidue = ItemId.Of("Desc_DarkEnergy_C");
    private static readonly ItemId PlutoniumWaste = ItemId.Of("Desc_PlutoniumWaste_C");
    private static readonly ItemId FicsoniumRod = ItemId.Of("Desc_FicsoniumFuelRod_C");

    [Fact]
    public void An_encoder_and_a_collider_that_feed_each_other_run_at_100_percent()
    {
        FlowResult result = Sweep(new TestFlow()
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

        Assert.Equal(1.0, result.Machine(Id("encoder")).Utilisation, Precision);
        Assert.Equal(1.0, result.Machine(Id("collider")).Utilisation, Precision);
    }
}
