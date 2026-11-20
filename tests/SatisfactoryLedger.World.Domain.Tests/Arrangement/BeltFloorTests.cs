using SatisfactoryLedger.World.Domain.Arrangement;

namespace SatisfactoryLedger.World.Domain.Tests.Arrangement;

/// <summary>
/// The belt floor below the factories (decided 2026-11-18), in squares of one foundation: a plot with its road is as
/// many squares as it is foundations, and a stack holds a belt per 2 metres of ceiling.
/// </summary>
public class BeltFloorTests
{
    // Plots of 6 foundations with roads of 4, both ways: 10 squares a plot with its road.
    private static readonly BeltFloor Floor = new(SquaresPerPlotX: 10, SquaresPerPlotY: 10, RoadSquaresX: 4, RoadSquaresY: 4, Levels: 16);

    [Fact]
    public void An_area_of_plots_is_their_squares_with_the_roads()
    {
        Assert.Equal(new GridRectangle(0, 10, 30, 20), Floor.SquaresOf(new GridRectangle(0, 1, 3, 2)));
    }

    [Fact]
    public void A_factory_stands_over_its_plots_and_the_roads_between_them_not_the_road_after_them()
    {
        Assert.Equal(new GridRectangle(10, 20, 16, 6), Floor.SquaresUnder(new GridRectangle(1, 2, 2, 1)));
    }

    [Fact]
    public void Plots_and_roads_in_metres_are_squares_of_one_foundation_and_a_stack_holds_a_belt_per_two_metres_of_ceiling()
    {
        Assert.Equal(Floor, BeltFloor.Of(plotWidthMetres: 48, plotDepthMetres: 48, roadXMetres: 32, roadYMetres: 32, ceilingMetres: 32));
    }

    [Fact]
    public void Levels_of_a_stack_are_two_metres_apart_from_the_floor_up()
    {
        Assert.Equal((0, 6), (BeltFloor.HeightOf(0), BeltFloor.HeightOf(3)));
    }

    [Fact]
    public void A_belt_ramps_up_or_down_two_metres_four_or_more_need_a_lift()
    {
        // Decided 2026-11-18.
        Assert.Equal((false, false, true, true), (BeltFloor.ClimbNeedsLift(0), BeltFloor.ClimbNeedsLift(1), BeltFloor.ClimbNeedsLift(2), BeltFloor.ClimbNeedsLift(-3)));
    }

    [Fact]
    public void A_low_ceiling_still_holds_one_belt()
    {
        Assert.Equal(1, BeltFloor.Of(48, 48, 32, 32, ceilingMetres: 1).Levels);
    }
}
