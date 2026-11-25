using SatisfactoryLedger.World.Domain.Arrangement;

namespace SatisfactoryLedger.World.Domain.Tests.Arrangement;

/// <summary>
/// A routed belt keeps its level from turn to turn; where it turns it may change level: by a ramp for 2 metres, by a
/// lift for 4 or more (decided 2026-11-18).
/// </summary>
public class RoutedBeltTests
{
    [Fact]
    public void Its_lifts_are_where_it_turns_four_metres_or_more_up_or_down()
    {
        // Level 0 east, a ramp up to 1 north, a lift up to 3 east, a lift down to 0 south.
        var belt = new RoutedBelt(
            "smelting",
            "plates",
            [new(0, 0), new(3, 0), new(3, 3), new(6, 3), new(6, 0)],
            LeavesFloor: false,
            Pipe: false,
            Levels: [0, 1, 3, 0],
            Index: 0,
            LastResortCrossings: []);

        Assert.Equal([new FloorSquare(3, 3), new FloorSquare(6, 3)], belt.Lifts);
    }
}
