using SatisfactoryLedger.World.Analysis;
using SatisfactoryLedger.World.Factories;

using static SatisfactoryLedger.World.Tests.TestWorldSnapshot;

namespace SatisfactoryLedger.World.Tests;

/// <summary>The findings of one factory in a snapshot (BR-A14), for the iron plate factory of the test world.</summary>
public class FactoryAnalysisTests
{
    private static readonly StoredFactory IronPlates = new()
    {
        Id = Guid.CreateVersion7(),
        World = "Development",
        Name = "Iron plates",
        OutputBelt = BeltIntoContainer,
        InputItems = ["Desc_OreIron_C"],
    };

    private static readonly FactoryAnalysisResponse Analysis = SnapshotAnalysis.ForFactory(Document, IronPlates, TestWorldAnalysis)!;

    [Fact]
    public void A_factory_says_what_it_makes_now_and_at_100_percent()
    {
        // Constructor C is clocked at 75%: 15 plates a minute at full speed, and it runs fully.
        Assert.Equal([new FactoryOutput("Desc_IronPlate_C", 15, 15)], Analysis.Output);
    }

    [Fact]
    public void The_summary_counts_only_the_factorys_machines()
    {
        Assert.Equal(new AnalysisSummary(Machines: 3, Running: 2, Starved: 0, OutputBlocked: 1, NoRecipe: 0), Analysis.Summary);
    }

    [Fact]
    public void Only_the_factorys_own_machines_are_machine_findings()
    {
        // Constructor D (starved) and the packager are in the world, but not in this factory.
        Assert.Equal(
            new HashSet<string> { SmelterA, SmelterB, ConstructorC },
            Analysis.Machines.Select(machine => machine.Id).ToHashSet());
    }

    [Fact]
    public void The_provider_feeding_the_factory_is_listed_with_its_unused_supply()
    {
        UnusedSupplyFinding miner = Assert.Single(Analysis.UnusedSupply);

        Assert.Equal((Miner, 22.5), (miner.Id, miner.Delivered));
    }

    [Fact]
    public void A_source_of_unknown_rate_that_feeds_another_factory_is_not_listed()
    {
        // The packager's canister container is in the world's findings, not in this factory's.
        Assert.Empty(Analysis.UnknownSupply);
    }

    [Fact]
    public void A_factory_whose_output_belt_is_not_in_the_snapshot_has_no_analysis()
    {
        StoredFactory gone = new() { Id = Guid.CreateVersion7(), World = "Development", Name = "Gone", OutputBelt = Id("Build_ConveyorBeltMk1_C_1"), InputItems = [] };

        Assert.Null(SnapshotAnalysis.ForFactory(Document, gone, TestWorldAnalysis));
    }
}
