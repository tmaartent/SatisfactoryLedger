namespace SatisfactoryLedger.World.Domain.Arrangement;

/// <summary>
/// Routes the belts between arranged factories on the floor below them (decided 2026-11-18):
/// <list type="bullet">
/// <item>A belt runs at right angles, square by square, from a lift under one factory to a lift under the other. A lift
/// takes its square: no other belt passes it.</item>
/// <item>Belts never cross seen from above, but one: a belt may cross over another where both run straight through
/// the square, on another level, so neither turns or changes level there (decided 2026-11-18). They may stack on a
/// square, one a level up to the floor's levels, and a stack keeps its order: a belt leaves it on the side it
/// joined.</item>
/// <item>A belt keeps its level from turn to turn; where it turns it may change level, by a ramp for 2 metres or a lift
/// for 4 or more (decided 2026-11-18).</item>
/// <item>Pipes are routed as belts are, but stack only with pipes: a stack is of stackable conveyor poles or of stackable
/// pipeline supports, 2 m a level either way (decided 2026-11-18).</item>
/// <item>Of the shortest ways a belt takes one with the fewest turns and crossings, then the fewest ramps and lifts. Links go shortest
/// first, one belt at a time; a belt that can't get through is reported, and so are the rest of its link's belts (or
/// pipes).</item>
/// <item>Rip-up and reroute (decided 2026-11-18): when belts can't get through, every belt is taken up and laid again,
/// the links that failed first, the most often failed foremost. The try with the fewest belts left over is kept.</item>
/// <item>Besides factories, a belt may end at a <em>spot</em>: where a node's extractor, a station or a drone port
/// stands, with a lift down there (decided 2026-11-18). A spot outside the area is reached at the floor's edge facing
/// it: the belt leaves the floor there, and the way still to go counts as belt.</item>
/// <item>Links with a factory outside the area aren't routed: it isn't on the floor.</item>
/// </list>
/// How it keeps belts from crossing: each side of a square holds the belts across it in order, and a belt through a
/// square joins two of its sides. Walking round a square, the belts through it must nest like brackets; two that
/// interleave cross, which only two running straight across each other may. That order is seen from above; apart from it, each belt has a level, one belt to a level on a
/// square. The search picks, side by side, where in each order a new belt goes, and at which level.
/// </summary>
public static class BeltRouter
{
    /// <summary>How often the belts are laid at most: the first time, and again after each try that left some over.</summary>
    private const int Tries = 8;

    /// <param name="places">Where the arranged factories stand, in plots.</param>
    /// <param name="spots">Where the nodes' extractors, stations and drone ports that links reach stand, in squares.</param>
    public static BeltRouting Route(
        BeltFloor floor,
        GridRectangle area,
        IReadOnlyDictionary<string, GridRectangle> places,
        IReadOnlyList<ArrangementLink> links,
        IReadOnlyDictionary<string, GridRectangle> spots)
    {
        ArgumentNullException.ThrowIfNull(floor);
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(spots);
        GridRectangle squares = floor.SquaresOf(area);

        // On the board, squares count from the area's lowest corner.
        GridRectangle OnBoard(GridRectangle at) => new(at.X - squares.X, at.Y - squares.Y, at.Width, at.Depth);
        var empty = new Board(squares.Width, squares.Depth, floor.Levels);
        Dictionary<string, End> ends = places.ToDictionary(place => place.Key, place => new End(OnBoard(floor.SquaresUnder(place.Value)), Outside: false));
        foreach ((string id, GridRectangle spot) in spots)
        {
            ends.TryAdd(id, empty.EndAt(OnBoard(spot)));
        }

        List<ArrangementLink> routable = [.. links
            .Where(link => link.BeltsAndPipes > 0 && ends.ContainsKey(link.From) && ends.ContainsKey(link.To))
            .OrderBy(link => Distance(ends[link.From].Space, ends[link.To].Space))];
        int[] failed = new int[routable.Count];
        BeltRouting? best = null;
        for (int attempt = 0; attempt < Tries; attempt++)
        {
            var board = new Board(squares.Width, squares.Depth, floor.Levels);
            List<RoutedBelt> belts = [];
            List<int> unrouted = [];
            List<ArrangementLink> leftOver = [];

            // The links that failed before go first; the rest, and ties, shortest first (OrderBy keeps their order).
            foreach (int index in Enumerable.Range(0, routable.Count).OrderByDescending(index => failed[index]).ToList())
            {
                ArrangementLink link = routable[index];
                (End from, End to) = (ends[link.From], ends[link.To]);
                int beltsLeft = Lay(link.Belts, pipe: false);
                int pipesLeft = Lay(link.Pipes, pipe: true);
                if (beltsLeft + pipesLeft > 0)
                {
                    unrouted.Add(index);
                    leftOver.Add(link with { Belts = beltsLeft, Pipes = pipesLeft });
                }

                // Lays a link's belts, or its pipes, one at a time: how many couldn't get through.
                int Lay(int count, bool pipe)
                {
                    for (int laid = 0; laid < count; laid++)
                    {
                        if (board.Find(from, to, pipe) is not { } way)
                        {
                            return count - laid;
                        }

                        (List<(int X, int Y)> corners, List<int> levels) = Runs(way.Squares, way.Levels, board.Width);
                        belts.Add(new RoutedBelt(
                            link.From,
                            link.To,
                            [.. corners.Select(corner => new FloorSquare(corner.X + squares.X, corner.Y + squares.Y))],
                            LeavesFloor: from.Outside || to.Outside,
                            pipe,
                            levels,
                            laid));
                    }

                    return 0;
                }
            }

            var routing = new BeltRouting(belts, leftOver, board.TallestStack);
            if (best is null || LeftOver(routing) < LeftOver(best))
            {
                best = routing;
            }

            if (unrouted.Count == 0)
            {
                break;
            }

            foreach (int index in unrouted)
            {
                failed[index]++;
            }
        }

        return best!;
    }

    private static int LeftOver(BeltRouting routing) => routing.Unrouted.Sum(link => link.BeltsAndPipes);

    /// <summary>Twice the distance along the grid between the centres.</summary>
    private static long Distance(GridRectangle a, GridRectangle b) =>
        Math.Abs(a.DoubleCentreX - b.DoubleCentreX) + Math.Abs(a.DoubleCentreY - b.DoubleCentreY);

    /// <summary>
    /// The first square, every square where the belt turns, and the last; and its level on each straight run between
    /// them. <paramref name="crossings"/> holds its level from each square to the next.
    /// </summary>
    private static (List<(int X, int Y)> Corners, List<int> Levels) Runs(List<int> path, List<int> crossings, int width)
    {
        List<(int X, int Y)> corners = [(path[0] % width, path[0] / width)];
        List<int> levels = [crossings[0]];
        for (int i = 1; i < path.Count - 1; i++)
        {
            if (path[i] - path[i - 1] != path[i + 1] - path[i])
            {
                corners.Add((path[i] % width, path[i] / width));
                levels.Add(crossings[i]);
            }
        }

        corners.Add((path[^1] % width, path[^1] / width));
        return (corners, levels);
    }
}

/// <summary>
/// The belt floor as the router fills it. Squares are numbered row by row. Sides of a square: 0 south (y − 1), 1 east
/// (x + 1), 2 north (y + 1), 3 west (x − 1). A side between two squares holds the belts across it in order: along x on
/// a south or north side, along y on an east or west side, seen from above. Their levels are kept per square.
/// </summary>
file sealed class Board(int width, int depth, int levels)
{
    private const int Lift = 4;

    // Belt work first: one square of belt outweighs every turn and climb a belt could make. Then turns, then climbs:
    // a ramp (2 m) before a lift (4 m or more). Last, low before high: a stack builds from the floor up.
    private const long Step = 1L << 32;
    private const long Turn = 1L << 16;
    private const long Cross = 2L << 16;
    private const long Ramp = 2L << 16;
    private const long LiftUp = 8L << 16;
    private const long High = 1;

    private static readonly (int X, int Y)[] Offsets = [(0, -1), (1, 0), (0, 1), (-1, 0)];

    private readonly int[] stacked = new int[width * depth];

    // Whether a square's stack is of pipes; it holds belts only otherwise.
    private readonly bool[] piped = new bool[width * depth];

    // Per square and level, whether a belt is there: a lift takes every level of its square.
    private readonly bool[] taken = new bool[width * depth * levels];

    // The side between (x, y) and (x + 1, y), and between (x, y) and (x, y + 1).
    private readonly List<int>?[] eastSides = new List<int>?[width * depth];
    private readonly List<int>?[] northSides = new List<int>?[width * depth];
    private int laid;

    public int Width => width;

    public int TallestStack => stacked.Length == 0 ? 0 : stacked.Max();

    /// <summary>
    /// An end on this board: a rectangle on the floor, clipped to it; or, for one wholly off the floor, the rectangle as
    /// it is, reached at the floor's edge.
    /// </summary>
    public End EndAt(GridRectangle space)
    {
        int x0 = Math.Max(0, space.X);
        int y0 = Math.Max(0, space.Y);
        int x1 = Math.Min(width, space.X + space.Width);
        int y1 = Math.Min(depth, space.Y + space.Depth);
        return x0 < x1 && y0 < y1 ? new End(new GridRectangle(x0, y0, x1 - x0, y1 - y0), Outside: false) : new End(space, Outside: true);
    }

    /// <summary>
    /// The way for a new belt (or pipe) with the least belt work, then the fewest turns, from a free square of
    /// <paramref name="from"/> to one of <paramref name="to"/>, with a level for each straight run and the fewest climbs;
    /// laid on the board; null when there's none. Its squares, and its level from each to the next. An end off the floor
    /// is reached at a free square of the floor's edge, and the way from there to it counts as belt.
    /// </summary>
    /// <remarks>
    /// The way is found first with only a free level asked of each square, and the levels picked along it after: searching
    /// every level everywhere took minutes on a real floor, mostly for belts with no way at all. Only when no levels fit
    /// the way found is the search made again, level by level.
    /// </remarks>
    public (List<int> Squares, List<int> Levels)? Find(End from, End to, bool pipe)
    {
        if (Search(from, to, pipe, levelled: false) is not { } way)
        {
            return null;
        }

        List<State>? states = Levelled(way) ?? Search(from, to, pipe, levelled: true);
        return states is null ? null : Lay(states, pipe);
    }

    /// <summary>
    /// The states of the cheapest way; null when there's none. <paramref name="levelled"/>: with a level for each,
    /// changing only where it turns; else at level 0 throughout, through squares with a level free.
    /// </summary>
    private List<State>? Search(End from, End to, bool pipe, bool levelled)
    {
        // A state: the square the belt is on, the side it came in by (or its lift), its place in that side's order, its
        // level (on its lift, the one it leaves at), and whether it ends there.
        Dictionary<State, long> cost = [];
        Dictionary<State, State?> before = [];
        var open = new PriorityQueue<State, (long Estimate, long Left)>();
        foreach (int square in Ends(from))
        {
            long beyond = from.Outside ? Step * DistanceTo(square, from.Space) : 0;
            long left = Step * DistanceTo(square, to.Space);
            for (int level = 0; level < (levelled ? levels : 1); level++)
            {
                var start = new State(square, Lift, 0, level);
                cost[start] = beyond + (High * level);
                before[start] = null;
                open.Enqueue(start, (cost[start] + left, left));
            }
        }

        while (open.TryDequeue(out State state, out (long Estimate, long Left) priority))
        {
            long here = cost[state];
            if (here + priority.Left != priority.Estimate)
            {
                // Reached more cheaply since it was queued.
                continue;
            }

            if (state.Ends)
            {
                List<State> states = [];
                for (State? at = state; at is { } s; at = before[s])
                {
                    states.Add(s);
                }

                states.Reverse();
                return states;
            }

            (int x, int y) = (state.Square % width, state.Square / width);
            for (int side = 0; side < 4; side++)
            {
                if (side == state.Side)
                {
                    continue;
                }

                (int nx, int ny) = (x + Offsets[side].X, y + Offsets[side].Y);
                if (nx < 0 || ny < 0 || nx >= width || ny >= depth)
                {
                    continue;
                }

                int next = (ny * width) + nx;
                bool arrives = IsEnd(next, to);
                bool turns = state.Side != Lift && side != Opposite(state.Side);

                // Arriving at the edge for an end off the floor, the way still to go from there counts too.
                long beyond = to.Outside ? Step * DistanceTo(next, to.Space) : 0;

                int slots = (Across(state.Square, side)?.Count ?? 0) + 1;
                for (int slot = 0; slot < slots; slot++)
                {
                    int crossed = state.Side == Lift ? 0 : Crossings(state.Square, state.Side, state.Slot, side, slot);
                    if (crossed < 0)
                    {
                        continue;
                    }

                    // Straight on, or off its lift, a belt keeps its level; where it turns it may change it.
                    (int lowest, int highest) = !levelled ? (0, 0) : turns ? (0, levels - 1) : (state.Level, state.Level);
                    for (int level = lowest; level <= highest; level++)
                    {
                        if (levelled && !Climbs(state.Square, state.Level, level))
                        {
                            continue;
                        }

                        long step = Step + (turns ? Turn : 0) + (Cross * crossed) + Climb(level - state.Level);
                        if (arrives)
                        {
                            Reach(new State(next, Opposite(side), slot, level, Ends: true), state, here + step + beyond, 0);
                        }

                        if ((levelled ? !taken[(next * levels) + level] : HasFreeLevel(next)) && (stacked[next] == 0 || piped[next] == pipe))
                        {
                            Reach(new State(next, Opposite(side), slot, level), state, here + step, Step * DistanceTo(next, to.Space));
                        }
                    }
                }
            }
        }

        return null;

        void Reach(State reached, State from, long total, long left)
        {
            if (cost.TryGetValue(reached, out long known) && known <= total)
            {
                return;
            }

            cost[reached] = total;
            before[reached] = from;
            open.Enqueue(reached, (total + left, left));
        }
    }

    private static int Opposite(int side) => (side + 2) % 4;

    /// <summary>
    /// The way with a level for each straight run: free on every square of it, changing where it turns only through
    /// levels free there; of those, the fewest and smallest climbs, low before high. Null when no levels fit.
    /// </summary>
    private List<State>? Levelled(List<State> way)
    {
        const long None = long.MaxValue;
        int last = way.Count - 1;
        List<int> corners = [0];
        for (int i = 1; i < last; i++)
        {
            if (way[i].Square - way[i - 1].Square != way[i + 1].Square - way[i].Square)
            {
                corners.Add(i);
            }
        }

        corners.Add(last);
        int runs = corners.Count - 1;
        long[,] best = new long[runs, levels];
        int[,] came = new int[runs, levels];
        for (int run = 0; run < runs; run++)
        {
            for (int level = 0; level < levels; level++)
            {
                best[run, level] = None;
                if (!Open(run, level))
                {
                    continue;
                }

                if (run == 0)
                {
                    best[run, level] = High * level;
                    continue;
                }

                for (int before = 0; before < levels; before++)
                {
                    if (best[run - 1, before] != None && Climbs(way[corners[run]].Square, before, level)
                        && best[run - 1, before] + Climb(level - before) < best[run, level])
                    {
                        best[run, level] = best[run - 1, before] + Climb(level - before);
                        came[run, level] = before;
                    }
                }
            }
        }

        int top = Enumerable.Range(0, levels).MinBy(level => best[runs - 1, level]);
        if (best[runs - 1, top] == None)
        {
            return null;
        }

        int[] picked = new int[runs];
        for (int run = runs - 1; run >= 0; run--)
        {
            picked[run] = top;
            top = came[run, top];
        }

        // A state's level is the one it came in at: from the square before it, on that square's run.
        int RunOf(int crossing) => corners.FindLastIndex(corner => corner <= crossing) is var run && run < runs ? run : runs - 1;
        return [.. way.Select((state, i) => state with { Level = picked[RunOf(Math.Max(0, i - 1))] })];

        // Whether a level is free on every square of a run but its lifts, which take their squares whole.
        bool Open(int run, int level)
        {
            for (int i = corners[run]; i <= corners[run + 1]; i++)
            {
                if (i != 0 && i != last && taken[(way[i].Square * levels) + level])
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>Whether a square has a level no belt takes yet.</summary>
    private bool HasFreeLevel(int square)
    {
        for (int level = 0; level < levels; level++)
        {
            if (!taken[(square * levels) + level])
            {
                return true;
            }
        }

        return false;
    }

    private static long Climb(int levels) => (levels == 0 ? 0 : BeltFloor.ClimbNeedsLift(levels) ? LiftUp : Ramp) + (High * Math.Abs(levels));

    /// <summary>Whether a belt on a square may go from one level to another there: every level between is free.</summary>
    private bool Climbs(int square, int from, int to)
    {
        for (int level = Math.Min(from, to); level <= Math.Max(from, to); level++)
        {
            if (level != from && taken[(square * levels) + level])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Lays a belt or pipe along its states: into each side's order at its place, and its levels on each square taken.
    /// Its ends are its lifts, which take their squares whole.
    /// </summary>
    private (List<int> Squares, List<int> Levels)? Lay(List<State> states, bool pipe)
    {
        List<int> squares = [.. states.Select(state => state.Square)];
        if (squares.Distinct().Count() != squares.Count)
        {
            // It would pass a square twice and might cross itself there: none of the tests found one, so it's simply refused.
            return null;
        }

        int belt = laid++;
        List<int> crossings = [];
        for (int i = 1; i < states.Count; i++)
        {
            int side = Opposite(states[i].Side);
            List<int> across = Across(states[i - 1].Square, side, create: true)!;
            across.Insert(states[i].Slot, belt);
            crossings.Add(states[i].Level);
        }

        for (int i = 0; i < states.Count; i++)
        {
            int square = states[i].Square;
            (int low, int high) = i == 0 || i == states.Count - 1
                ? (0, levels - 1)
                : (Math.Min(states[i].Level, states[i + 1].Level), Math.Max(states[i].Level, states[i + 1].Level));
            for (int level = low; level <= high; level++)
            {
                taken[(square * levels) + level] = true;
            }

            stacked[square]++;
            piped[square] = pipe;
        }

        return (squares, crossings);
    }

    /// <summary>
    /// How many belts a belt crosses that comes into a square by one side at a place in its order, and leaves by another
    /// at a place in that one's; -1 when it may not go so. Walking round the square, every belt shows twice; two that
    /// interleave cross. Only a belt running straight through may cross, and only belts running straight across it: on
    /// another level, which the levels taken on the square see to (decided 2026-11-18). A belt that ends on the square, at
    /// its lift, shows only once, so nothing may pass it: that's what keeps every other belt off a lift.
    /// </summary>
    private int Crossings(int square, int inSide, int inSlot, int outSide, int outSlot)
    {
        const int New = -1;
        List<(int Belt, int Side)> round = [];
        for (int side = 0; side < 4; side++)
        {
            List<int> belts = [.. Across(square, side) ?? []];
            if (side == inSide)
            {
                belts.Insert(inSlot, New);
            }

            if (side == outSide)
            {
                belts.Insert(outSlot, New);
            }

            // Round the square anticlockwise: south and east sides in their order, north and west against it.
            if (side >= 2)
            {
                belts.Reverse();
            }

            round.AddRange(belts.Select(belt => (belt, side)));
        }

        int first = round.FindIndex(entry => entry.Belt == New);
        int second = round.FindLastIndex(entry => entry.Belt == New);
        bool straight = outSide == Opposite(inSide);
        int crossed = 0;
        foreach (IGrouping<int, (int Index, int Side)> belt in round
            .Select((entry, index) => (entry.Belt, Index: index, entry.Side))
            .Where(entry => entry.Belt != New)
            .GroupBy(entry => entry.Belt, entry => (entry.Index, entry.Side)))
        {
            (int Index, int Side)[] ends = [.. belt];
            if (ends.Length != 2)
            {
                return -1;
            }

            bool inside = first < ends[0].Index && ends[0].Index < second;
            if (inside == (first < ends[1].Index && ends[1].Index < second))
            {
                continue;
            }

            // They interleave: a crossing, allowed only straight over straight, at right angles.
            if (!straight || ends[1].Side != Opposite(ends[0].Side) || ends[0].Side % 2 == inSide % 2)
            {
                return -1;
            }

            crossed++;
        }

        return crossed;
    }

    /// <summary>The belts across a side of a square, in order; null when there are none, or it's the floor's edge.</summary>
    private List<int>? Across(int square, int side, bool create = false)
    {
        (int x, int y) = (square % width, square / width);
        (List<int>?[] sides, int index, bool inside) = side switch
        {
            0 => (northSides, square - width, y > 0),
            1 => (eastSides, square, x < width - 1),
            2 => (northSides, square, y < depth - 1),
            _ => (eastSides, square - 1, x > 0),
        };
        if (!inside)
        {
            return null;
        }

        if (create)
        {
            sides[index] ??= [];
        }

        return sides[index];
    }

    /// <summary>A square a lift may take: no belt on it yet.</summary>
    private bool Free(int square) => stacked[square] == 0;

    /// <summary>Where a belt may end: a free square of the end, or of the floor's edge for an end off the floor.</summary>
    private bool IsEnd(int square, End end) => Free(square) && (end.Outside ? OnEdge(square) : Within(square, end.Space));

    private IEnumerable<int> Ends(End end) => SquaresIn(new GridRectangle(0, 0, width, depth)).Where(square => IsEnd(square, end));

    private bool OnEdge(int square)
    {
        (int x, int y) = (square % width, square / width);
        return x == 0 || y == 0 || x == width - 1 || y == depth - 1;
    }

    private bool Within(int square, GridRectangle rectangle)
    {
        (int x, int y) = (square % width, square / width);
        return x >= rectangle.X && y >= rectangle.Y && x < rectangle.X + rectangle.Width && y < rectangle.Y + rectangle.Depth;
    }

    private int DistanceTo(int square, GridRectangle rectangle)
    {
        (int x, int y) = (square % width, square / width);
        int dx = Math.Max(0, Math.Max(rectangle.X - x, x - (rectangle.X + rectangle.Width - 1)));
        int dy = Math.Max(0, Math.Max(rectangle.Y - y, y - (rectangle.Y + rectangle.Depth - 1)));
        return dx + dy;
    }

    private IEnumerable<int> SquaresIn(GridRectangle rectangle)
    {
        for (int y = Math.Max(0, rectangle.Y); y < Math.Min(depth, rectangle.Y + rectangle.Depth); y++)
        {
            for (int x = Math.Max(0, rectangle.X); x < Math.Min(width, rectangle.X + rectangle.Width); x++)
            {
                yield return (y * width) + x;
            }
        }
    }
}

/// <summary>
/// Where a belt being routed is: on a square, come in by a side (or its lift), at a place in that side's order and at a
/// level; and whether it ends there.
/// </summary>
file readonly record struct State(int Square, int Side, int Slot, int Level, bool Ends = false);

/// <summary>Where a belt may start or end on the board: under a factory or at a spot; or off the floor, reached at its edge.</summary>
file sealed record End(GridRectangle Space, bool Outside);
