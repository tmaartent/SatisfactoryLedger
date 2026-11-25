using SatisfactoryLedger.World.Domain.Arrangement;

namespace SatisfactoryLedger.World.Domain.Tests.Arrangement;

/// <summary>
/// The belts between arranged factories, routed on the floor below them (decided 2026-11-18): at right angles, from a
/// lift under one factory to a lift under the other, never crossing seen from above. Belts may stack, and a stack keeps
/// its order; a lift takes its square on the levels it runs through. A belt that can't get through cleanly may cross
/// another where one turns, or part from a stack swapped, as a last resort (step 102). One that can't even so is reported.
/// </summary>
/// <remarks>The tests' plots are one square each, without roads, so a plot is a square.</remarks>
public class BeltRouterTests
{
    private static BeltRouting Route(
        GridRectangle area, Dictionary<string, GridRectangle> places, ArrangementLink[] links, int levels = 16, Dictionary<string, GridRectangle>? spots = null) =>
        BeltRouter.Route(new BeltFloor(1, 1, 0, 0, levels), area, places, links, spots ?? []);

    /// <summary>The belt's level on a square of one of its straight runs.</summary>
    private static int LevelAt(RoutedBelt belt, FloorSquare square)
    {
        for (int run = 0; run < belt.Levels.Count; run++)
        {
            (FloorSquare a, FloorSquare b) = (belt.Corners[run], belt.Corners[run + 1]);
            if (square.X >= Math.Min(a.X, b.X) && square.X <= Math.Max(a.X, b.X) && square.Y >= Math.Min(a.Y, b.Y) && square.Y <= Math.Max(a.Y, b.Y))
            {
                return belt.Levels[run];
            }
        }

        throw new ArgumentException($"{square} isn't on the belt", nameof(square));
    }

    private static int Length(RoutedBelt belt) =>
        belt.Corners.Zip(belt.Corners.Skip(1), (a, b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y)).Sum();

    [Fact]
    public void A_belt_runs_straight_from_a_lift_under_one_factory_to_a_lift_under_the_other()
    {
        BeltRouting routing = Route(
            new GridRectangle(0, 0, 10, 3),
            new() { ["smelting"] = new(0, 1, 1, 1), ["plates"] = new(9, 1, 1, 1) },
            [new ArrangementLink("smelting", "plates", Belts: 1)]);

        RoutedBelt belt = Assert.Single(routing.Belts);
        Assert.Equal(("smelting", "plates"), (belt.From, belt.To));
        Assert.Equal([new FloorSquare(0, 1), new FloorSquare(9, 1)], belt.Corners);
        Assert.Empty(belt.LastResortCrossings);
    }

    [Fact]
    public void Of_the_shortest_ways_a_belt_takes_one_with_the_fewest_turns()
    {
        BeltRouting routing = Route(
            new GridRectangle(0, 0, 7, 5),
            new() { ["smelting"] = new(0, 0, 1, 1), ["plates"] = new(6, 4, 1, 1), ["wire"] = new(3, 0, 1, 1), ["cable"] = new(4, 0, 1, 1) },
            [new ArrangementLink("smelting", "plates", Belts: 1), new ArrangementLink("wire", "cable", Belts: 1)]);

        // Wire's and cable's lifts stand in the way east; a way round them is as short as north then east, with more turns.
        RoutedBelt belt = routing.Belts.Single(belt => belt.From == "smelting");
        Assert.Equal((10, 3), (Length(belt), belt.Corners.Count));
    }

    [Fact]
    public void A_link_gets_as_many_belts_as_it_needs()
    {
        BeltRouting routing = Route(
            new GridRectangle(0, 0, 6, 2),
            new() { ["smelting"] = new(0, 0, 1, 2), ["plates"] = new(5, 0, 1, 2) },
            [new ArrangementLink("smelting", "plates", Belts: 2)]);

        Assert.Equal(2, routing.Belts.Count);
        Assert.Empty(routing.Unrouted);
    }

    [Fact]
    public void Every_belt_has_a_lift_of_its_own_so_a_factory_has_room_for_as_many_as_it_has_squares()
    {
        BeltRouting routing = Route(
            new GridRectangle(0, 0, 6, 1),
            new() { ["smelting"] = new(0, 0, 1, 1), ["plates"] = new(5, 0, 1, 1) },
            [new ArrangementLink("smelting", "plates", Belts: 2)]);

        Assert.Single(routing.Belts);
        Assert.Equal([new ArrangementLink("smelting", "plates", 1)], routing.Unrouted);
    }

    [Fact]
    public void A_belt_never_passes_a_lift_on_the_levels_it_takes()
    {
        // The short link goes first, on the floor: its lifts rise from there up to the factories, through every level,
        // and close the only row.
        BeltRouting routing = Route(
            new GridRectangle(0, 0, 6, 1),
            new() { ["smelting"] = new(0, 0, 1, 1), ["ingots"] = new(2, 0, 1, 1), ["rods"] = new(3, 0, 1, 1), ["plates"] = new(5, 0, 1, 1) },
            [new ArrangementLink("smelting", "plates", 1), new ArrangementLink("ingots", "rods", 1)]);

        Assert.Equal(("ingots", "rods"), (Assert.Single(routing.Belts).From, routing.Belts[0].To));
        Assert.Equal([new ArrangementLink("smelting", "plates", 1)], routing.Unrouted);
    }

    [Fact]
    public void A_belt_passes_under_a_factory_s_lift_below_the_level_its_belt_leaves_at()
    {
        // Step 102 (decided 2026-11-25): a lift under a factory rises from its belt's level to the factory. Assemblers'
        // belt crosses over the short north-south one, so it runs a level up, and its lift at (2, 2) leaves the floor
        // level free. The long belt north runs straight through under it.
        BeltRouting routing = Route(
            new GridRectangle(0, 0, 8, 7),
            new()
            {
                ["ingots"] = new(4, 1, 1, 1), ["rods"] = new(4, 3, 1, 1), ["assemblers"] = new(2, 2, 1, 1), ["frames"] = new(7, 2, 1, 1),
                ["smelting"] = new(2, 0, 1, 1), ["plates"] = new(2, 6, 1, 1),
            },
            [new ArrangementLink("ingots", "rods", 1), new ArrangementLink("assemblers", "frames", 1), new ArrangementLink("smelting", "plates", 1)]);

        RoutedBelt assemblers = routing.Belts.Single(belt => belt.From == "assemblers");
        RoutedBelt smelting = routing.Belts.Single(belt => belt.From == "smelting");
        Assert.Equal(1, LevelAt(assemblers, new FloorSquare(3, 2)));
        Assert.Equal([new FloorSquare(2, 0), new FloorSquare(2, 6)], smelting.Corners);
        Assert.Equal(0, LevelAt(smelting, new FloorSquare(2, 2)));
    }

    [Fact]
    public void A_belt_passes_over_a_spot_s_lift_above_the_level_its_belt_comes_in_at()
    {
        // Step 102 (decided 2026-11-25): a lift at an extractor or a station runs down from its belt's level to the
        // ground. The miner's belt to the station takes the floor level of the only row; the long belt passes above.
        BeltRouting routing = Route(
            new GridRectangle(0, 0, 6, 1),
            new() { ["smelting"] = new(0, 0, 1, 1), ["plates"] = new(5, 0, 1, 1) },
            [new ArrangementLink("smelting", "plates", 1), new ArrangementLink("miner", "freight platform", 1)],
            spots: new() { ["miner"] = new(2, 0, 1, 1), ["freight platform"] = new(3, 0, 1, 1) });

        RoutedBelt smelting = routing.Belts.Single(belt => belt.From == "smelting");
        Assert.Empty(routing.Unrouted);
        Assert.Equal([1], smelting.Levels);
        Assert.Empty(smelting.LastResortCrossings);
    }

    [Fact]
    public void A_belt_crosses_over_another_where_both_run_straight_on_another_level()
    {
        // Smelting west, plates east, ingots north, rods south: the two links cross in the middle (decided 2026-11-18).
        BeltRouting routing = Route(
            new GridRectangle(0, 0, 3, 3),
            new() { ["smelting"] = new(0, 1, 1, 1), ["plates"] = new(2, 1, 1, 1), ["ingots"] = new(1, 2, 1, 1), ["rods"] = new(1, 0, 1, 1) },
            [new ArrangementLink("smelting", "plates", 1), new ArrangementLink("ingots", "rods", 1)]);

        Assert.Empty(routing.Unrouted);
        Assert.NotEqual(LevelAt(routing.Belts[0], new FloorSquare(1, 1)), LevelAt(routing.Belts[1], new FloorSquare(1, 1)));
        Assert.All(routing.Belts, belt => Assert.Empty(belt.LastResortCrossings));
    }

    [Fact]
    public void Belts_never_cross_on_one_level_one_that_cannot_go_around_is_reported()
    {
        // As above, under a ceiling of one level.
        BeltRouting routing = Route(
            new GridRectangle(0, 0, 3, 3),
            new() { ["smelting"] = new(0, 1, 1, 1), ["plates"] = new(2, 1, 1, 1), ["ingots"] = new(1, 2, 1, 1), ["rods"] = new(1, 0, 1, 1) },
            [new ArrangementLink("smelting", "plates", 1), new ArrangementLink("ingots", "rods", 1)],
            levels: 1);

        Assert.Single(routing.Belts);
        Assert.Single(routing.Unrouted);
    }

    /// <summary>Lifts close every way along the middle row but one, where the belt from the north turns twice.</summary>
    private static readonly Dictionary<string, GridRectangle> AroundATurn = new()
    {
        ["smelting"] = new(0, 1, 1, 1), ["plates"] = new(4, 1, 1, 1), ["ingots"] = new(1, 2, 1, 1), ["rods"] = new(2, 0, 1, 1),
        ["wire"] = new(1, 0, 1, 1), ["cable"] = new(0, 0, 1, 1), ["screws"] = new(2, 2, 1, 1), ["bolts"] = new(3, 2, 1, 1),
    };

    private static readonly ArrangementLink[] AroundATurnLinks =
    [
        new ArrangementLink("smelting", "plates", 1), new ArrangementLink("ingots", "rods", 1),
        new ArrangementLink("wire", "cable", 1), new ArrangementLink("screws", "bolts", 1),
    ];

    [Fact]
    public void A_straight_belt_crosses_another_where_that_one_turns_only_as_a_last_resort_on_another_level()
    {
        // Ingots to rods, the shorter, goes first: down from the north, east along the middle row, south again. Smelting
        // to plates along the middle row can't get by without crossing it where it turns (step 102, decided 2026-11-25).
        BeltRouting routing = Route(new GridRectangle(0, 0, 5, 3), AroundATurn, AroundATurnLinks);

        RoutedBelt smelting = routing.Belts.Single(belt => belt.From == "smelting");
        RoutedBelt ingots = routing.Belts.Single(belt => belt.From == "ingots");
        FloorSquare crossing = Assert.Single(smelting.LastResortCrossings);
        Assert.Empty(routing.Unrouted);
        Assert.Contains(crossing, ingots.Corners);
        Assert.NotEqual(LevelAt(smelting, crossing), LevelAt(ingots, crossing));
        Assert.Equal(1, routing.LastResortCrossings);
    }

    [Fact]
    public void A_last_resort_crossing_needs_a_level_of_its_own_on_one_level_the_belt_is_reported()
    {
        BeltRouting routing = Route(new GridRectangle(0, 0, 5, 3), AroundATurn, AroundATurnLinks, levels: 1);

        Assert.Equal(1, routing.Unrouted.Sum(link => link.BeltsAndPipes));
        Assert.Equal(0, routing.LastResortCrossings);
    }

    [Fact]
    public void A_turning_belt_crosses_a_straight_one_only_as_a_last_resort_on_another_level()
    {
        // Smelting to plates runs along the middle row. Ingots, north of it, can only reach rods, south-east, by turning
        // east on the middle row and south again: crossing smelting's belt where it turns. Lifts close every other way.
        BeltRouting routing = Route(
            new GridRectangle(0, 0, 4, 3),
            new()
            {
                ["smelting"] = new(0, 1, 1, 1), ["plates"] = new(3, 1, 1, 1), ["ingots"] = new(1, 2, 1, 1), ["rods"] = new(2, 0, 1, 1),
                ["wire"] = new(1, 0, 1, 1), ["cable"] = new(0, 0, 1, 1), ["screws"] = new(2, 2, 1, 1), ["bolts"] = new(3, 2, 1, 1),
            },
            AroundATurnLinks);

        RoutedBelt smelting = routing.Belts.Single(belt => belt.From == "smelting");
        RoutedBelt ingots = routing.Belts.Single(belt => belt.From == "ingots");
        FloorSquare crossing = Assert.Single(ingots.LastResortCrossings);
        Assert.Empty(routing.Unrouted);
        Assert.Empty(smelting.LastResortCrossings);
        Assert.Contains(crossing, ingots.Corners);
        Assert.NotEqual(LevelAt(smelting, crossing), LevelAt(ingots, crossing));
    }

    [Fact]
    public void A_clean_way_goes_before_a_shorter_one_that_crosses_as_a_last_resort()
    {
        // As the straight belt above, with a free row along the north: smelting goes round by it, twice as far.
        BeltRouting routing = Route(new GridRectangle(0, 0, 5, 4), AroundATurn, AroundATurnLinks);

        RoutedBelt smelting = routing.Belts.Single(belt => belt.From == "smelting");
        Assert.Empty(routing.Unrouted);
        Assert.Equal(0, routing.LastResortCrossings);
        Assert.Equal(8, Length(smelting));
    }

    [Fact]
    public void Two_belts_in_a_stack_part_swapped_only_as_a_last_resort_on_another_level()
    {
        // Lifts close the north and south rows between the corners: both long belts stack along the middle row, the one
        // from the south-west on its south side. It leaves north, the other south, so they part swapped (step 102).
        BeltRouting routing = Route(
            new GridRectangle(0, 0, 5, 3),
            new()
            {
                ["smelting"] = new(0, 0, 1, 1), ["plates"] = new(4, 2, 1, 1), ["ingots"] = new(0, 2, 1, 1), ["rods"] = new(4, 0, 1, 1),
                ["wire"] = new(1, 0, 1, 1), ["cable"] = new(2, 0, 1, 1), ["screws"] = new(1, 2, 1, 1), ["bolts"] = new(2, 2, 1, 1),
                ["nuts"] = new(3, 0, 1, 1), ["bars"] = new(3, 2, 1, 1),
            },
            [
                new ArrangementLink("smelting", "plates", 1), new ArrangementLink("ingots", "rods", 1),
                new ArrangementLink("wire", "cable", 1), new ArrangementLink("screws", "bolts", 1), new ArrangementLink("nuts", "bars", 1),
            ]);

        RoutedBelt smelting = routing.Belts.Single(belt => belt.From == "smelting");
        RoutedBelt ingots = routing.Belts.Single(belt => belt.From == "ingots");
        FloorSquare crossing = Assert.Single(smelting.LastResortCrossings.Concat(ingots.LastResortCrossings));
        Assert.Empty(routing.Unrouted);
        // Where they join the stack or leave it: along it, a stack keeps its order.
        Assert.Contains(crossing, new[] { new FloorSquare(0, 1), new FloorSquare(4, 1) });
        Assert.NotEqual(LevelAt(smelting, crossing), LevelAt(ingots, crossing));
    }

    [Fact]
    public void Two_belts_in_a_stack_never_swap_sides_one_crosses_over_the_other_outside_it()
    {
        // The short links' lifts leave one gap in the middle, so the long belts must stack through it. Smelting (south-west)
        // to plates (north-east) and ingots (north-west) to rods (south-east) would leave that stack swapped; instead
        // ingots crosses over smelting's belt west of the gap, where both run straight (decided 2026-11-18).
        BeltRouting routing = Route(
            new GridRectangle(0, 0, 7, 3),
            new()
            {
                ["smelting"] = new(0, 0, 1, 1), ["plates"] = new(6, 2, 1, 1), ["ingots"] = new(0, 2, 1, 1), ["rods"] = new(6, 0, 1, 1),
                ["wire"] = new(3, 0, 1, 1), ["cable"] = new(4, 0, 1, 1), ["screws"] = new(3, 2, 1, 1), ["bolts"] = new(4, 2, 1, 1),
            },
            [
                new ArrangementLink("smelting", "plates", 1), new ArrangementLink("ingots", "rods", 1),
                new ArrangementLink("wire", "cable", 1), new ArrangementLink("screws", "bolts", 1),
            ]);

        RoutedBelt smelting = routing.Belts.Single(belt => belt.From == "smelting");
        RoutedBelt ingots = routing.Belts.Single(belt => belt.From == "ingots");
        Assert.Empty(routing.Unrouted);
        Assert.NotEqual(LevelAt(smelting, new FloorSquare(1, 1)), LevelAt(ingots, new FloorSquare(1, 1)));
        Assert.Equal(0, routing.LastResortCrossings);
    }

    [Fact]
    public void Belts_stack_where_there_is_no_room_beside_them()
    {
        // The short link's lifts take the bottom row in the middle: both belts of the long one pass above, stacked.
        BeltRouting routing = Route(
            new GridRectangle(0, 0, 6, 2),
            new() { ["smelting"] = new(0, 0, 1, 2), ["ingots"] = new(2, 0, 1, 1), ["rods"] = new(3, 0, 1, 1), ["plates"] = new(5, 0, 1, 2) },
            [new ArrangementLink("smelting", "plates", 2), new ArrangementLink("ingots", "rods", 1)]);

        Assert.Equal(3, routing.Belts.Count);
        Assert.Equal(2, BeltFloor.LevelsUsed(routing.Belts.Select(belt => belt.Levels)));
    }

    [Fact]
    public void Belts_stacked_on_a_square_are_on_levels_of_their_own()
    {
        // As above: both long belts pass over (2, 1) and (3, 1).
        BeltRouting routing = Route(
            new GridRectangle(0, 0, 6, 2),
            new() { ["smelting"] = new(0, 0, 1, 2), ["ingots"] = new(2, 0, 1, 1), ["rods"] = new(3, 0, 1, 1), ["plates"] = new(5, 0, 1, 2) },
            [new ArrangementLink("smelting", "plates", 2), new ArrangementLink("ingots", "rods", 1)]);

        RoutedBelt[] stacked = [.. routing.Belts.Where(belt => belt.From == "smelting")];
        Assert.Equal([0, 1], stacked.Select(belt => LevelAt(belt, new FloorSquare(2, 1))).Order());
    }

    [Fact]
    public void A_belt_keeps_its_level_from_turn_to_turn_and_climbs_only_when_it_must()
    {
        BeltRouting routing = Route(
            new GridRectangle(0, 0, 7, 5),
            new() { ["smelting"] = new(0, 0, 1, 1), ["plates"] = new(6, 4, 1, 1), ["wire"] = new(3, 0, 1, 1), ["cable"] = new(4, 0, 1, 1) },
            [new ArrangementLink("smelting", "plates", Belts: 1), new ArrangementLink("wire", "cable", Belts: 1)]);

        RoutedBelt belt = routing.Belts.Single(belt => belt.From == "smelting");
        Assert.Equal(belt.Corners.Count - 1, belt.Levels.Count);
        Assert.Single(belt.Levels.Distinct());
    }

    [Fact]
    public void A_stack_holds_no_more_belts_than_fit_under_the_ceiling()
    {
        BeltRouting routing = Route(
            new GridRectangle(0, 0, 6, 2),
            new() { ["smelting"] = new(0, 0, 1, 2), ["ingots"] = new(2, 0, 1, 1), ["rods"] = new(3, 0, 1, 1), ["plates"] = new(5, 0, 1, 2) },
            [new ArrangementLink("smelting", "plates", 2), new ArrangementLink("ingots", "rods", 1)],
            levels: 1);

        Assert.Equal([new ArrangementLink("smelting", "plates", 1)], routing.Unrouted);
        Assert.Equal(1, BeltFloor.LevelsUsed(routing.Belts.Select(belt => belt.Levels)));
    }

    [Fact]
    public void A_link_with_a_factory_outside_the_area_is_not_routed_its_belts_leave_the_floor()
    {
        BeltRouting routing = Route(
            new GridRectangle(0, 0, 6, 2),
            new() { ["plates"] = new(5, 0, 1, 2) },
            [new ArrangementLink("outside", "plates", 1)]);

        Assert.Empty(routing.Belts);
        Assert.Empty(routing.Unrouted);
    }

    [Fact]
    public void A_belt_from_a_node_comes_up_a_lift_at_its_extractor()
    {
        BeltRouting routing = Route(
            new GridRectangle(0, 0, 6, 3),
            new() { ["smelting"] = new(5, 1, 1, 1) },
            [new ArrangementLink("miner", "smelting", 1)],
            spots: new() { ["miner"] = new(0, 1, 1, 1) });

        RoutedBelt belt = Assert.Single(routing.Belts);
        Assert.Equal([new FloorSquare(0, 1), new FloorSquare(5, 1)], belt.Corners);
        Assert.False(belt.LeavesFloor);
    }

    [Fact]
    public void A_belt_to_a_station_goes_down_a_lift_at_its_platform()
    {
        BeltRouting routing = Route(
            new GridRectangle(0, 0, 6, 3),
            new() { ["plates"] = new(0, 0, 1, 3) },
            [new ArrangementLink("plates", "freight platform", 2)],
            spots: new() { ["freight platform"] = new(4, 0, 2, 3) });

        Assert.Equal(2, routing.Belts.Count);
        Assert.All(routing.Belts, belt => Assert.True(belt.Corners[^1].X >= 4, $"ends at {belt.Corners[^1]}"));
    }

    [Fact]
    public void A_belt_from_a_spot_outside_the_area_leaves_the_floor_at_the_edge_facing_it()
    {
        // The miner stands far west of the floor, level with its second row, as smelting in the east does.
        BeltRouting routing = Route(
            new GridRectangle(0, 0, 6, 4),
            new() { ["smelting"] = new(5, 1, 1, 1) },
            [new ArrangementLink("miner", "smelting", 1)],
            spots: new() { ["miner"] = new(-20, 1, 1, 1) });

        RoutedBelt belt = Assert.Single(routing.Belts);
        Assert.Equal((new FloorSquare(0, 1), true), (belt.Corners[0], belt.LeavesFloor));
    }

    [Fact]
    public void A_link_s_pipes_are_routed_as_pipes()
    {
        BeltRouting routing = Route(
            new GridRectangle(0, 0, 6, 2),
            new() { ["water"] = new(0, 0, 1, 2), ["refinery"] = new(5, 0, 1, 2) },
            [new ArrangementLink("water", "refinery", Belts: 1, Pipes: 1)]);

        Assert.Equal([false, true], routing.Belts.Select(belt => belt.Pipe).Order());
    }

    [Fact]
    public void A_link_s_belts_are_numbered_from_0_and_so_are_its_pipes()
    {
        BeltRouting routing = Route(
            new GridRectangle(0, 0, 6, 3),
            new() { ["refinery"] = new(0, 0, 1, 3), ["plastic"] = new(5, 0, 1, 3) },
            [new ArrangementLink("refinery", "plastic", Belts: 2, Pipes: 1)]);

        Assert.Equal([0, 1], routing.Belts.Where(belt => !belt.Pipe).Select(belt => belt.Index).Order());
        Assert.Equal([0], routing.Belts.Where(belt => belt.Pipe).Select(belt => belt.Index));
    }

    [Fact]
    public void Pipes_stack_with_pipes()
    {
        // As belts stack: the short link's lifts take the bottom row in the middle, both pipes pass above.
        BeltRouting routing = Route(
            new GridRectangle(0, 0, 6, 2),
            new() { ["water"] = new(0, 0, 1, 2), ["ingots"] = new(2, 0, 1, 1), ["rods"] = new(3, 0, 1, 1), ["refinery"] = new(5, 0, 1, 2) },
            [new ArrangementLink("water", "refinery", Belts: 0, Pipes: 2), new ArrangementLink("ingots", "rods", 1)]);

        Assert.Equal((3, 2), (routing.Belts.Count, BeltFloor.LevelsUsed(routing.Belts.Select(belt => belt.Levels))));
    }

    [Fact]
    public void A_pipe_never_joins_a_stack_of_belts()
    {
        // The same squares, but a belt and a pipe: stackable pipeline supports and conveyor poles don't mix (decided
        // 2026-11-18), so one of them can't get past.
        BeltRouting routing = Route(
            new GridRectangle(0, 0, 6, 2),
            new() { ["water"] = new(0, 0, 1, 2), ["ingots"] = new(2, 0, 1, 1), ["rods"] = new(3, 0, 1, 1), ["refinery"] = new(5, 0, 1, 2) },
            [new ArrangementLink("water", "refinery", Belts: 1, Pipes: 1), new ArrangementLink("ingots", "rods", 1)]);

        Assert.Equal(2, routing.Belts.Count);
        Assert.Equal(1, Assert.Single(routing.Unrouted).BeltsAndPipes);
        Assert.Equal(1, BeltFloor.LevelsUsed(routing.Belts.Select(belt => belt.Levels)));
    }

    [Fact]
    public void A_pipe_crosses_over_a_belt_where_both_run_straight_on_another_level()
    {
        // Water west to the refinery east, ingots north to rods south: they cross in the middle (decided 2026-11-18).
        BeltRouting routing = Route(
            new GridRectangle(0, 0, 3, 3),
            new() { ["water"] = new(0, 1, 1, 1), ["refinery"] = new(2, 1, 1, 1), ["ingots"] = new(1, 2, 1, 1), ["rods"] = new(1, 0, 1, 1) },
            [new ArrangementLink("water", "refinery", Belts: 0, Pipes: 1), new ArrangementLink("ingots", "rods", 1)]);

        Assert.Empty(routing.Unrouted);
        Assert.NotEqual(LevelAt(routing.Belts[0], new FloorSquare(1, 1)), LevelAt(routing.Belts[1], new FloorSquare(1, 1)));
    }

    [Fact]
    public void A_pipe_never_crosses_a_belt_on_one_level()
    {
        BeltRouting routing = Route(
            new GridRectangle(0, 0, 3, 3),
            new() { ["water"] = new(0, 1, 1, 1), ["refinery"] = new(2, 1, 1, 1), ["ingots"] = new(1, 2, 1, 1), ["rods"] = new(1, 0, 1, 1) },
            [new ArrangementLink("water", "refinery", Belts: 0, Pipes: 1), new ArrangementLink("ingots", "rods", 1)],
            levels: 1);

        Assert.Single(routing.Unrouted);
    }

    private static SupplyLeg Leg(string from, string to, bool injection = false, int? onto = null, int? off = null) =>
        new(from, to, "Desc_OreIron_C", Pipe: false, 300, injection, Overflow: off is not null, onto, off);

    /// <summary>A leg of a line running on past a factory, from a splitter on the leg into it.</summary>
    private static SupplyLeg RunOn(string from, string to, int off) =>
        new(from, to, "Desc_OreIron_C", Pipe: false, 300, Injection: false, Overflow: false, Off: off);

    private static BeltRouting RouteLines(GridRectangle area, Dictionary<string, GridRectangle> places, params SupplyLeg[] legs) =>
        BeltRouter.RouteLines(new BeltFloor(1, 1, 0, 0, 16), area, places, legs, new Dictionary<string, GridRectangle>());

    /// <summary>The squares a belt passes, corner to corner.</summary>
    private static List<FloorSquare> Squares(RoutedBelt belt)
    {
        List<FloorSquare> squares = [belt.Corners[0]];
        foreach ((FloorSquare a, FloorSquare b) in belt.Corners.Zip(belt.Corners.Skip(1)))
        {
            for (int step = 1; step <= Math.Abs(b.X - a.X) + Math.Abs(b.Y - a.Y); step++)
            {
                squares.Add(new FloorSquare(a.X + (Math.Sign(b.X - a.X) * step), a.Y + (Math.Sign(b.Y - a.Y) * step)));
            }
        }

        return squares;
    }

    [Fact]
    public void A_top_up_belt_ends_on_the_lines_belt_before_the_factory_at_a_priority_merger()
    {
        // The line runs east along the middle row to the smelters; more miners stand north of it (decided 2026-11-18).
        BeltRouting routing = RouteLines(
            new GridRectangle(0, 0, 10, 5),
            new() { ["miners"] = new(0, 2, 1, 1), ["smelters"] = new(9, 2, 1, 1), ["more"] = new(5, 4, 1, 1) },
            Leg("miners", "smelters"),
            Leg("more", "smelters", injection: true, onto: 0));

        RoutedBelt line = routing.Belts.Single(belt => belt.From == "miners");
        RoutedBelt topUp = routing.Belts.Single(belt => belt.From == "more");
        Assert.Empty(routing.Unrouted);
        Assert.True(topUp.EndsOnBelt);
        Assert.Contains(topUp.Corners[^1], Squares(line)[1..^1]);
        Assert.Equal(LevelAt(line, topUp.Corners[^1]), topUp.Levels[^1]);
        Assert.Equal(new FloorSquare(9, 2), line.Corners[^1]);
    }

    [Fact]
    public void A_top_up_joins_the_line_anywhere_along_it_nearest_where_it_comes_from()
    {
        // A long line east; the more miners stand by its start, far from the factory (2026-11-18: joins only near the
        // factory found no room on short or crowded belts, and the top-up went up into the factory instead).
        BeltRouting routing = RouteLines(
            new GridRectangle(0, 0, 20, 5),
            new() { ["miners"] = new(0, 2, 1, 1), ["smelters"] = new(19, 2, 1, 1), ["more"] = new(3, 4, 1, 1) },
            Leg("miners", "smelters"),
            Leg("more", "smelters", injection: true, onto: 0));

        RoutedBelt topUp = routing.Belts.Single(belt => belt.From == "more");
        Assert.True(topUp.EndsOnBelt);
        Assert.Equal(new FloorSquare(3, 2), topUp.Corners[^1]);
    }

    [Fact]
    public void A_top_up_boxed_out_of_its_line_is_laid_right_after_it_on_another_try()
    {
        // Two shorter belts run beside the line, north and south, on the floor, so they're laid first: the top-up can't
        // reach the line on its level, and falls back to the factory. On another try the line and its top-up go first
        // (decided 2026-11-18), and the two cross over the top-up instead.
        BeltRouting routing = RouteLines(
            new GridRectangle(0, 0, 12, 7),
            new()
            {
                ["miners"] = new(0, 3, 1, 1), ["smelters"] = new(11, 2, 1, 3), ["more"] = new(5, 6, 1, 1),
                ["a"] = new(1, 2, 1, 1), ["b"] = new(10, 2, 1, 1), ["c"] = new(1, 4, 1, 1), ["d"] = new(10, 4, 1, 1),
            },
            Leg("miners", "smelters"),
            Leg("more", "smelters", injection: true, onto: 0),
            Leg("a", "b"),
            Leg("c", "d"));

        Assert.Empty(routing.Unrouted);
        Assert.True(routing.Belts.Single(belt => belt.From == "more").EndsOnBelt);
    }

    [Fact]
    public void A_top_ups_overflow_leaves_its_belt_at_a_smart_splitter()
    {
        // The top-up comes down from the north onto the line; its overflow runs on west to a factory of its own.
        BeltRouting routing = RouteLines(
            new GridRectangle(0, 0, 10, 7),
            new() { ["miners"] = new(0, 2, 1, 1), ["smelters"] = new(9, 2, 1, 1), ["more"] = new(5, 6, 1, 1), ["plates"] = new(0, 4, 1, 1) },
            Leg("miners", "smelters"),
            Leg("more", "smelters", injection: true, onto: 0),
            Leg("smelters", "plates", off: 1));

        RoutedBelt topUp = routing.Belts.Single(belt => belt.From == "more");
        RoutedBelt overflow = routing.Belts.Single(belt => belt.To == "plates");
        Assert.Empty(routing.Unrouted);
        Assert.True(overflow.StartsOnBelt);
        Assert.Contains(overflow.Corners[0], Squares(topUp)[1..^1]);
    }

    [Fact]
    public void A_line_runs_on_past_a_factory_from_a_splitter_on_the_floor_not_from_the_factory()
    {
        // The line runs east to the smelters; the plates are fed after them, south of the line (decided 2026-11-19).
        BeltRouting routing = RouteLines(
            new GridRectangle(0, 0, 10, 5),
            new() { ["miners"] = new(0, 2, 1, 1), ["smelters"] = new(9, 2, 1, 1), ["plates"] = new(6, 0, 1, 1) },
            Leg("miners", "smelters"),
            RunOn("smelters", "plates", off: 0));

        RoutedBelt line = routing.Belts.Single(belt => belt.From == "miners");
        RoutedBelt runOn = routing.Belts.Single(belt => belt.To == "plates");
        Assert.Empty(routing.Unrouted);
        Assert.True(runOn.StartsOnBelt);
        Assert.Contains(runOn.Corners[0], Squares(line)[1..^1]);
        Assert.Equal(new FloorSquare(9, 2), line.Corners[^1]);
    }

    [Fact]
    public void A_line_runs_on_from_its_splitter_only_after_its_priority_merger()
    {
        // The plates, fed after the smelters, stand south by the line's start; the top-up joins near its end. Split off
        // before the merger, the plates would miss what the top-up brings: the splitter comes after it, and the top-up is
        // laid first, though its way is longer (decided 2026-11-19).
        BeltRouting routing = RouteLines(
            new GridRectangle(0, 0, 13, 12),
            new() { ["miners"] = new(0, 2, 1, 1), ["smelters"] = new(12, 2, 1, 1), ["more"] = new(10, 11, 1, 1), ["plates"] = new(4, 0, 1, 1) },
            Leg("miners", "smelters"),
            Leg("more", "smelters", injection: true, onto: 0),
            RunOn("smelters", "plates", off: 0));

        RoutedBelt line = routing.Belts.Single(belt => belt.From == "miners");
        RoutedBelt topUp = routing.Belts.Single(belt => belt.From == "more");
        RoutedBelt runOn = routing.Belts.Single(belt => belt.To == "plates");
        List<FloorSquare> along = Squares(line);
        Assert.Empty(routing.Unrouted);
        Assert.True(topUp.EndsOnBelt);
        Assert.True(runOn.StartsOnBelt);
        Assert.True(along.IndexOf(runOn.Corners[0]) > along.IndexOf(topUp.Corners[^1]));
    }

    [Fact]
    public void A_belt_to_a_spot_outside_leaves_the_floor_where_its_existing_belt_comes_in()
    {
        // The miner stands far east, but its belt in the save comes in at the north edge, above the smelters
        // (decided 2026-11-18): the belt leaves the floor there, not at the east edge facing the miner.
        BeltRouting routing = BeltRouter.Route(
            new BeltFloor(1, 1, 0, 0, 16),
            new GridRectangle(0, 0, 10, 5),
            new Dictionary<string, GridRectangle> { ["smelters"] = new(2, 1, 1, 1) },
            [new ArrangementLink("miner", "smelters", Belts: 1)],
            new Dictionary<string, GridRectangle> { ["miner"] = new(30, 2, 1, 1) },
            new Dictionary<string, GridRectangle> { ["miner"] = new(1, 4, 3, 2) });

        RoutedBelt belt = Assert.Single(routing.Belts);
        Assert.True(belt.LeavesFloor);
        Assert.Equal(4, belt.Corners[0].Y);
        Assert.InRange(belt.Corners[0].X, 1, 3);
    }

    [Fact]
    public void A_belt_whose_existing_belt_comes_in_beyond_the_floor_runs_along_the_floor_to_the_edge_by_it()
    {
        // The drawn area reaches past the floor (whole plots): the miner's belt comes in east of it, by the top rows. The
        // belt runs along the floor to the edge there, not to the edge by the smelters (2026-11-18: it left by its
        // factory, and the rest of the way was a line across the map).
        BeltRouting routing = BeltRouter.Route(
            new BeltFloor(1, 1, 0, 0, 16),
            new GridRectangle(0, 0, 10, 5),
            new Dictionary<string, GridRectangle> { ["smelters"] = new(2, 0, 1, 1) },
            [new ArrangementLink("miner", "smelters", Belts: 1)],
            new Dictionary<string, GridRectangle> { ["miner"] = new(30, 4, 1, 1) },
            new Dictionary<string, GridRectangle> { ["miner"] = new(11, 3, 3, 2) });

        RoutedBelt belt = Assert.Single(routing.Belts);
        Assert.True(belt.LeavesFloor);
        Assert.Equal(9, belt.Corners[0].X);
        Assert.InRange(belt.Corners[0].Y, 3, 4);
    }

    [Fact]
    public void A_belt_that_cannot_get_through_is_routed_first_on_another_try()
    {
        // Shortest first, ingots to rods runs straight over (2, 1), the only square of the plates' lift. Routed first,
        // plates' belt goes its way and ingots' goes round below.
        BeltRouting routing = Route(
            new GridRectangle(0, 0, 5, 4),
            new() { ["ingots"] = new(1, 0, 1, 2), ["rods"] = new(3, 1, 1, 1), ["plates"] = new(2, 1, 1, 1), ["screws"] = new(0, 3, 1, 1) },
            [new ArrangementLink("ingots", "rods", 1), new ArrangementLink("plates", "screws", 1)]);

        Assert.Empty(routing.Unrouted);
        Assert.Equal(2, routing.Belts.Count);
    }
}
