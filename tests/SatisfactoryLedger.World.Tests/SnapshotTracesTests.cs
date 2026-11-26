using SatisfactoryLedger.World.Analysis;
using SatisfactoryLedger.World.Contracts.Analysis;
using SatisfactoryLedger.World.Domain.Tracing;
using SatisfactoryLedger.World.Tracing;

using static SatisfactoryLedger.World.Tests.TestWorldSnapshot;

namespace SatisfactoryLedger.World.Tests;

/// <summary>
/// Where shortages and blockages start in the test world (decided 2026-11-20), over its analysis: the rod constructor gets
/// no ingots at all; smelter A can't get all its ingots away, because constructor C, which takes them, also takes 10 a
/// minute from smelter B.
/// </summary>
public class SnapshotTracesTests
{
    private const string Constructor = "Build_ConstructorMk1_C";
    private const string Smelter = "Build_SmelterMk1_C";

    [Fact]
    public void A_constructor_no_belt_brings_ingots_to_has_nothing_bringing_them()
    {
        TraceResponse trace = SnapshotTraces.Shortage(Document, TestWorldAnalysis, Constructor, "Recipe_IronRod_C", []);

        TraceStartResponse start = Assert.Single(trace.Starts);
        Assert.Equal((TraceCause.NothingBringsIt, "Desc_IronIngot_C"), (start.Cause, start.Item));
        Assert.Equal((1, 1), (trace.Machines, trace.Traced));
    }

    [Fact]
    public void Each_step_groups_the_machines_traced_by_class_and_recipe_with_where_the_first_stands()
    {
        TraceStepResponse step = Assert.Single(SnapshotTraces.Shortage(Document, TestWorldAnalysis, Constructor, "Recipe_IronRod_C", []).Starts[0].Path);

        TraceGroupResponse rods = Assert.Single(step.Machines);
        Assert.Equal((Constructor, "Recipe_IronRod_C", 1), (rods.ClassName, rods.Recipe, rods.Count));
        Assert.NotNull(rods.Position);
        Assert.Equal(15, step.PerMinute);
    }

    [Fact]
    public void A_blocked_smelter_whose_consumer_also_takes_from_another_smelter_is_supplied_by_others()
    {
        TraceResponse trace = SnapshotTraces.Blockage(Document, TestWorldAnalysis, Smelter, null, []);

        TraceStartResponse start = Assert.Single(trace.Starts);
        Assert.Equal(TraceCause.SuppliedByOthers, start.Cause);
        Assert.Equal((Constructor, 22.5), (Assert.Single(start.At).ClassName, start.PerMinute));
        TraceGroupResponse other = Assert.Single(start.Others);
        Assert.Equal((Smelter, SmelterB, 10.0), (other.ClassName, other.Ids[0], other.PerMinute));
        Assert.Equal((2, 1), (trace.Machines, trace.Traced));
    }

    [Fact]
    public void Machines_that_run_as_they_should_give_nothing_to_trace()
    {
        TraceResponse trace = SnapshotTraces.Shortage(Document, TestWorldAnalysis, Constructor, "Recipe_IronPlate_C", []);

        Assert.Equal((1, 0), (trace.Machines, trace.Traced));
        Assert.Empty(trace.Starts);
    }

    [Fact]
    public void The_Catalog_s_tiers_are_read_by_kind_leaving_out_what_is_no_belt_lift_or_pipe()
    {
        IReadOnlyList<TransportTier> tiers = SnapshotTraces.Tiers(
            [new("Build_ConveyorBeltMk6_C", 1200), new("Build_ConveyorLiftMk6_C", 1200), new("Build_PipelineMK2_NoIndicator_C", 600), new("Build_Hypertube_C", 0)]);

        Assert.Equal(
            [
                new TransportTier("Build_ConveyorBeltMk6_C", Domain.LogisticsKind.Belt, 1200),
                new TransportTier("Build_ConveyorLiftMk6_C", Domain.LogisticsKind.Lift, 1200),
                new TransportTier("Build_PipelineMK2_NoIndicator_C", Domain.LogisticsKind.Pipe, 600),
            ],
            tiers);
    }

    [Fact]
    public void The_stored_analysis_reads_as_the_trace_needs_it()
    {
        AnalysisDocument analysis = TestWorldAnalysis;

        FlowReadings readings = AnalysisReadings.Of(Document.ToWorldGraph(), analysis);

        MachineReading smelter = readings.Machine(Domain.BuildableId.Of(SmelterA))!;
        Assert.Equal(Domain.Flow.Objects.MachineLimit.OutputBlocked, smelter.Limit);
        Assert.Contains(Domain.ItemId.Of("Desc_IronIngot_C"), smelter.BlockedOn.Keys);
    }
}
