using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Domain.Tracing;

namespace SatisfactoryLedger.World.Domain.Tests.Tracing;

/// <summary>
/// What a bottleneck needs to carry what's offered to it (decided 2026-11-22): the lowest tier of its kind that carries
/// it alone; when even the highest tier can't, the highest tier in as many lines as it takes.
/// </summary>
public class MoreCapacityTests
{
    private static readonly TransportTier[] Tiers =
    [
        new("Build_ConveyorBeltMk3_C", LogisticsKind.Belt, 270),
        new("Build_ConveyorBeltMk4_C", LogisticsKind.Belt, 480),
        new("Build_ConveyorBeltMk5_C", LogisticsKind.Belt, 780),
        new("Build_ConveyorBeltMk6_C", LogisticsKind.Belt, 1200),
        new("Build_ConveyorLiftMk5_C", LogisticsKind.Lift, 780),
        new("Build_ConveyorLiftMk6_C", LogisticsKind.Lift, 1200),
        new("Build_Pipeline_C", LogisticsKind.Pipe, 300),
        new("Build_PipelineMK2_C", LogisticsKind.Pipe, 600),
        new("Build_PipelineMK2_NoIndicator_C", LogisticsKind.Pipe, 600),
    ];

    private static Logistics Conduit(LogisticsKind kind, int capacity) => new(BuildableId.Of("conduit"), kind, Rate.PerMinute(capacity));

    [Fact]
    public void Below_the_highest_tier_the_lowest_tier_that_carries_what_is_offered_is_enough()
    {
        MoreCapacity? more = MoreCapacity.For(Conduit(LogisticsKind.Belt, 270), offeredMore: 200, Tiers);

        Assert.Equal(new MoreCapacity("Build_ConveyorBeltMk4_C", 1, 470), more);
    }

    [Fact]
    public void At_the_highest_tier_it_takes_as_many_lines_as_carry_what_is_offered()
    {
        MoreCapacity? more = MoreCapacity.For(Conduit(LogisticsKind.Belt, 1200), offeredMore: 1300, Tiers);

        Assert.Equal(new MoreCapacity("Build_ConveyorBeltMk6_C", 3, 2500), more);
    }

    [Fact]
    public void Below_the_highest_tier_when_even_the_highest_cannot_carry_it_alone_it_takes_lines_of_the_highest()
    {
        MoreCapacity? more = MoreCapacity.For(Conduit(LogisticsKind.Belt, 780), offeredMore: 1000, Tiers);

        Assert.Equal(new MoreCapacity("Build_ConveyorBeltMk6_C", 2, 1780), more);
    }

    [Fact]
    public void A_lift_takes_lift_tiers()
    {
        Assert.Equal("Build_ConveyorLiftMk6_C", MoreCapacity.For(Conduit(LogisticsKind.Lift, 780), offeredMore: 100, Tiers)?.Tier);
    }

    [Fact]
    public void A_pipe_network_takes_pipe_tiers_preferring_the_pipeline_with_indicators()
    {
        Assert.Equal("Build_PipelineMK2_C", MoreCapacity.For(Conduit(LogisticsKind.PipeNetwork, 300), offeredMore: 100, Tiers)?.Tier);
    }

    [Fact]
    public void Without_tiers_of_its_kind_there_is_nothing_to_suggest()
    {
        Assert.Null(MoreCapacity.For(Conduit(LogisticsKind.Belt, 780), offeredMore: 100, []));
    }
}
