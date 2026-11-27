using SatisfactoryLedger.World.Analysis;
using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.Domain.Flow.Objects;

using static SatisfactoryLedger.World.Tests.TestWorldSnapshot;
using MachineLimit = SatisfactoryLedger.World.Contracts.Analysis.MachineLimit;

namespace SatisfactoryLedger.World.Tests;

/// <summary>The findings of a snapshot (BR-A9, BR-A11 to A13), as the API gives them, for the test world.</summary>
public class SnapshotAnalysisTests
{
    private static readonly AnalysisResponse Analysis = SnapshotAnalysis.For(Document, TestWorldAnalysis);

    [Fact]
    public void The_summary_counts_the_machines_by_how_they_run()
    {
        // Smelter B, constructor C and the packager run fully; smelter A is blocked; constructor D has no input.
        Assert.Equal(new AnalysisSummary(Machines: 5, Running: 3, Starved: 1, OutputBlocked: 1, NoRecipe: 0), Analysis.Summary);
    }

    [Fact]
    public void A_pipe_an_output_feeds_carries_its_own_flow_in_the_model()
    {
        PipeFlowFinding pipe = Assert.Single(Analysis.PipeFlows, flow => flow.Id == Id("Build_Pipeline_C_2147257932"));

        // From the water extractor to the packager, which takes 60 m³ a minute.
        Assert.Equal((false, 60.0, 300.0), (pipe.OfNetwork, pipe.PerMinute["Desc_Water_C"], pipe.Capacity));
    }

    [Fact]
    public void A_pipe_inside_a_network_shows_what_its_network_takes_in_in_the_model()
    {
        PipeFlowFinding pipe = Assert.Single(Analysis.PipeFlows, flow => flow.Id == Id("Build_PipelineMK2_C_2147409670"));

        // Past the pump: its network takes in 300 m³ a minute, over the pipe before the pump.
        Assert.Equal((true, 300.0), (pipe.OfNetwork, pipe.PerMinute["Desc_Water_C"]));
    }

    [Fact]
    public void A_belt_has_no_pipe_flow()
    {
        Assert.DoesNotContain(Analysis.PipeFlows, flow => flow.Id.Contains("ConveyorBelt", StringComparison.Ordinal));
    }

    [Fact]
    public void The_test_world_settles_so_nothing_is_approximate()
    {
        Assert.True(Analysis.Settled);
        Assert.Empty(Analysis.ApproximateParts);
    }

    [Fact]
    public void A_machine_below_100_percent_is_a_finding_with_its_cause_and_where_it_stands()
    {
        MachineFinding smelterA = Assert.Single(Analysis.Machines, machine => machine.Id == SmelterA);

        Assert.Equal(("Recipe_IngotIron_C", 0.4167, MachineLimit.OutputBlocked, "Desc_IronIngot_C"),
            (smelterA.Recipe, smelterA.Utilisation, smelterA.Limit, smelterA.LimitingItem));
        Assert.NotNull(smelterA.Position);
        Assert.False(smelterA.Approximate);
    }

    [Fact]
    public void A_machine_running_fully_is_listed_without_a_cause()
    {
        MachineFinding smelterB = Assert.Single(Analysis.Machines, machine => machine.Id == SmelterB);

        Assert.Equal((1.0, MachineLimit.None, (string?)null), (smelterB.Utilisation, smelterB.Limit, smelterB.LimitingItem));
    }

    [Fact]
    public void Every_machine_is_listed_the_one_running_worst_first()
    {
        Assert.Equal(
            ["Build_ConstructorMk1_C", "Build_SmelterMk1_C", "Build_ConstructorMk1_C", "Build_Packager_C", "Build_SmelterMk1_C"],
            Analysis.Machines.Select(machine => machine.ClassName));
    }

    [Fact]
    public void A_provider_delivering_less_than_it_offers_is_unused_supply()
    {
        UnusedSupplyFinding miner = Assert.Single(Analysis.UnusedSupply, supply => supply.Id == Miner);

        Assert.Equal(("Desc_OreIron_C", 60, 22.5), (miner.Item, miner.Offered, miner.Delivered));
    }

    [Fact]
    public void A_source_of_unknown_rate_says_what_it_supplies_and_whether_its_stock_lasts()
    {
        // The packager's canister container is empty now: assumed supplied, and its stock tells nothing.
        UnknownSupplyFinding container = Assert.Single(Analysis.UnknownSupply, supply => supply.Id == Id("Build_StorageContainerMk2_C_2147239114"));

        Assert.Equal(60, container.Delivered["Desc_FluidCanister_C"]);
        Assert.Null(container.StockMinutes);
        Assert.False(container.Confirmed);
    }

    [Fact]
    public void A_source_or_destination_that_rounds_to_nothing_on_the_page_is_not_listed()
    {
        // Per minute, unrounded: what the factory page shows to 0.0001 a minute (BR-A10), and leaves out at 0 (#52).
        (string Name, double PerMinute)[] handovers = [("smelter", 12.34567), ("next to nothing", 0.00004), ("just enough", 0.00005)];

        Assert.Equal(
            [("smelter", 12.3457), ("just enough", 0.0001)],
            SnapshotAnalysis.Shown(handovers, handover => handover.PerMinute).Select(shown => (shown.Value.Name, shown.PerMinute)));
    }
}
