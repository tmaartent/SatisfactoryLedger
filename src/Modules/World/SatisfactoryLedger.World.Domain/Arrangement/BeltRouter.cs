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
/// pipeline supports, 2 m a level either way. A pipe may cross over a belt as a belt does another: both straight through
/// the square, on another level (decided 2026-11-18).</item>
/// <item>Of the shortest ways a belt takes one with the fewest turns and crossings, then the fewest ramps and lifts. Links go shortest
/// first, one belt at a time; a belt that can't get through is reported, and so are the rest of its link's belts (or
/// pipes).</item>
/// <item>Rip-up and reroute (decided 2026-11-18): when belts can't get through, every belt is taken up and laid again,
/// the links that failed first, the most often failed foremost. The try with the fewest belts left over is kept.</item>
/// <item>Besides factories, a belt may end at a <em>spot</em>: where a node's extractor, a station or a drone port
/// stands, with a lift down there (decided 2026-11-18). A spot outside the area is reached at the floor's edge facing
/// it: the belt leaves the floor there, and the way still to go counts as belt. Where an existing belt in the save comes
/// into the area from it, the belt leaves the floor there instead, to join that one (its <em>entry</em>).</item>
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
    /// <param name="entries">For a spot outside the area, where its existing belt comes into the area, in squares.</param>
    public static BeltRouting Route(
        BeltFloor floor,
        GridRectangle area,
        IReadOnlyDictionary<string, GridRectangle> places,
        IReadOnlyList<ArrangementLink> links,
        IReadOnlyDictionary<string, GridRectangle> spots,
        IReadOnlyDictionary<string, GridRectangle>? entries = null)
    {
        ArgumentNullException.ThrowIfNull(links);
        List<Job> jobs = [];
        foreach ((ArrangementLink link, int number) in links.Select((link, number) => (link, number)))
        {
            jobs.AddRange(Enumerable.Range(0, link.Belts).Select(index => new Job(link, number, Pipe: false, index)));
            jobs.AddRange(Enumerable.Range(0, link.Pipes).Select(index => new Job(link, number, Pipe: true, index)));
        }

        return Lay(floor, area, places, spots, entries, jobs);
    }

    /// <summary>
    /// The legs of supply lines (decided 2026-11-18): each a belt of its own, numbered as the legs are. A top-up ends on the
    /// line's belt before the factory, at a priority merger; an overflow starts on the top-up's belt, at a smart splitter;
    /// a line running on past a factory starts on the leg into it, at a splitter after its mergers (decided 2026-11-19).
    /// Each takes a square of the other belt, but its ends, where that belt is alone, and joins it at its level from a side
    /// it doesn't use; the square is then taken whole, as a lift's. Where none fits, it goes to the factory as before.
    /// </summary>
    /// <param name="places">Where the arranged factories stand, in plots.</param>
    /// <param name="spots">Where the nodes' extractors, stations and drone ports that the legs reach stand, in squares.</param>
    /// <param name="entries">For a spot outside the area, where its existing belt comes into the area, in squares.</param>
    public static BeltRouting RouteLines(
        BeltFloor floor,
        GridRectangle area,
        IReadOnlyDictionary<string, GridRectangle> places,
        IReadOnlyList<SupplyLeg> legs,
        IReadOnlyDictionary<string, GridRectangle> spots,
        IReadOnlyDictionary<string, GridRectangle>? entries = null)
    {
        ArgumentNullException.ThrowIfNull(legs);
        return Lay(floor, area, places, spots, entries, [.. legs.Select((leg, number) => new Job(
            new ArrangementLink(leg.From, leg.To, leg.Pipe ? 0 : 1, Pipes: leg.Pipe ? 1 : 0), number, leg.Pipe, number, leg.Onto, leg.Off))]);
    }

    private static BeltRouting Lay(
        BeltFloor floor,
        GridRectangle area,
        IReadOnlyDictionary<string, GridRectangle> places,
        IReadOnlyDictionary<string, GridRectangle> spots,
        IReadOnlyDictionary<string, GridRectangle>? entries,
        List<Job> jobs)
    {
        ArgumentNullException.ThrowIfNull(floor);
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(spots);
        GridRectangle squares = floor.SquaresOf(area);

        // On the board, squares count from the area's lowest corner.
        GridRectangle OnBoard(GridRectangle at) => new(at.X - squares.X, at.Y - squares.Y, at.Width, at.Depth);
        var empty = new Board(squares.Width, squares.Depth, floor.Levels);
        Dictionary<string, End> ends = places.ToDictionary(place => place.Key, place => new End(OnBoard(floor.SquaresUnder(place.Value)), Outside: false));
        foreach ((string id, GridRectangle spot) in spots)
        {
            // Where its existing belt comes in, the belt leaves the floor for it: on the floor's edge there, or facing it.
            ends.TryAdd(id, entries?.GetValueOrDefault(id) is { } entry ? empty.EdgeAt(OnBoard(entry)) : empty.EndAt(OnBoard(spot)));
        }

        // A belt that joins another is laid after it: lines, then their top-ups, then the overflows off those. One leaving
        // a belt at a splitter is laid after the top-ups onto that belt too, so it can leave after their mergers.
        IEnumerable<int> Before(int job) =>
            new[] { jobs[job].Onto, jobs[job].Off }.OfType<int>()
                .Concat(jobs[job].Off is int off ? Enumerable.Range(0, job).Where(other => jobs[other].Onto == off) : [])
                .Where(other => other < job);
        int[] depth = new int[jobs.Count];
        for (int job = 0; job < jobs.Count; job++)
        {
            depth[job] = Before(job).Select(other => depth[other] + 1).DefaultIfEmpty(0).Max();
        }

        List<int> routable = [.. Enumerable.Range(0, jobs.Count)
            .Where(job => ends.ContainsKey(jobs[job].Link.From) && ends.ContainsKey(jobs[job].Link.To))
            .OrderBy(job => depth[job])
            .ThenBy(job => Distance(ends[jobs[job].Link.From].Space, ends[jobs[job].Link.To].Space))];
        int[] failed = new int[jobs.Count];

        // Per join, how often it found no room on the belt it joins and went to the factory instead: like a belt left
        // over, it's tried again, laid right after what it joins (decided 2026-11-18).
        int[] fellBack = new int[jobs.Count];
        HashSet<int> routableJobs = [.. routable];
        IEnumerable<int> Joined(int job) => Before(job).Where(routableJobs.Contains);
        BeltRouting? best = null;
        int bestFallbacks = 0;
        for (int attempt = 0; attempt < Tries; attempt++)
        {
            var board = new Board(squares.Width, squares.Depth, floor.Levels);
            List<RoutedBelt> belts = [];
            Dictionary<int, (List<int> Squares, List<int> Levels)> laid = [];

            // Where each top-up laid ends: on the belt it joins, at the square of its merger; null where it fell back.
            Dictionary<int, int?> merged = [];
            List<int> unrouted = [];
            List<int> fallbacks = [];

            // A link's belts (or pipes) after one that couldn't get through are reported with it.
            HashSet<(int Link, bool Pipe)> stopped = [];

            // The joins that fell back before go first, each right after what it joins (and what that joins); then the
            // belts that failed before, joins after what they join; the rest, and ties, shortest first.
            List<int> order = [];
            HashSet<int> placed = [];
            void Front(int job)
            {
                if (placed.Contains(job))
                {
                    return;
                }

                foreach (int other in Joined(job))
                {
                    Front(other);
                }

                placed.Add(job);
                order.Add(job);
            }

            foreach (int job in routable.Where(job => fellBack[job] > 0).OrderByDescending(job => fellBack[job]).ToList())
            {
                Front(job);
            }

            order.AddRange(routable.OrderBy(job => depth[job]).ThenByDescending(job => failed[job]).Where(job => !placed.Contains(job)));

            // A splitter leaves a belt after the mergers onto it, so what they bring runs on too; none where a merger fell
            // back to the factory, since what it brings then only reaches the factory: the belt leaves from there.
            End? SplitterOn(int off, (List<int> Squares, List<int> Levels) belt)
            {
                List<int?> mergers = [.. merged.Where(top => jobs[top.Key].Onto == off).Select(top => top.Value)];
                return mergers.Contains(null) ? null : board.JointsOn(belt, after: mergers.Select(square => belt.Squares.IndexOf(square!.Value)).DefaultIfEmpty(0).Max());
            }

            foreach (int index in order)
            {
                Job job = jobs[index];
                if (stopped.Contains((job.Number, job.Pipe)))
                {
                    unrouted.Add(index);
                    continue;
                }

                (End from, End to) = (ends[job.Link.From], ends[job.Link.To]);
                End? splitter = job.Off is int off && laid.TryGetValue(off, out var top) ? SplitterOn(off, top) : null;
                End? merger = job.Onto is int onto && laid.TryGetValue(onto, out var under) ? board.JointsOn(under) : null;
                (List<int> Squares, List<int> Levels)? way = board.Find(splitter ?? from, merger ?? to, job.Pipe);
                if (way is null && (splitter ?? merger) is not null)
                {
                    (splitter, merger) = (null, null);
                    way = board.Find(from, to, job.Pipe);
                }

                if (way is not { } found)
                {
                    stopped.Add((job.Number, job.Pipe));
                    unrouted.Add(index);
                    continue;
                }

                laid[index] = found;
                if (job.Onto is not null)
                {
                    merged[index] = merger is null ? null : found.Squares[^1];
                }

                if ((job.Onto is not null && merger is null) || (job.Off is not null && splitter is null))
                {
                    fallbacks.Add(index);
                }

                (List<(int X, int Y)> corners, List<int> levels) = Runs(found.Squares, found.Levels, board.Width);
                belts.Add(new RoutedBelt(
                    job.Link.From,
                    job.Link.To,
                    [.. corners.Select(corner => new FloorSquare(corner.X + squares.X, corner.Y + squares.Y))],
                    LeavesFloor: (splitter is null && (from.Outside || from.Leaves)) || (merger is null && (to.Outside || to.Leaves)),
                    job.Pipe,
                    levels,
                    job.Index,
                    StartsOnBelt: splitter is not null,
                    EndsOnBelt: merger is not null));
            }

            List<ArrangementLink> leftOver = [.. unrouted
                .GroupBy(job => jobs[job].Number)
                .Select(link => jobs[link.First()].Link with { Belts = link.Count(job => !jobs[job].Pipe), Pipes = link.Count(job => jobs[job].Pipe) })];
            var routing = new BeltRouting(belts, leftOver, board.TallestStack);

            // The fewest belts left over, then the fewest joins that fell back to the factory.
            if (best is null || (LeftOver(routing), fallbacks.Count).CompareTo((LeftOver(best), bestFallbacks)) < 0)
            {
                (best, bestFallbacks) = (routing, fallbacks.Count);
            }

            if (unrouted.Count == 0 && fallbacks.Count == 0)
            {
                break;
            }

            foreach (int index in unrouted)
            {
                failed[index]++;
            }

            foreach (int index in fallbacks)
            {
                fellBack[index]++;
            }
        }

        return best!;
    }

    /// <summary>
    /// One belt (or pipe) to lay: of a link, at its number among the link's (or for a supply line's leg, among the legs);
    /// and for a leg, the leg it merges onto or whose splitter it leaves, by their numbers.
    /// </summary>
    private sealed record Job(ArrangementLink Link, int Number, bool Pipe, int Index, int? Onto = null, int? Off = null);

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

    // Per belt laid, whether it's a pipe.
    private readonly List<bool> pipes = [];

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
    /// Where a belt leaves the floor to join an existing one that comes in there: the squares of the floor nearest it,
    /// on its edge when it comes in beyond the floor (the drawn area reaches past the whole plots), so the belt runs along
    /// the floor to there rather than to any square of the edge.
    /// </summary>
    public End EdgeAt(GridRectangle entry)
    {
        static int Clamp(int value, int size) => Math.Clamp(value, 0, size - 1);
        (int x0, int y0) = (Clamp(entry.X, width), Clamp(entry.Y, depth));
        (int x1, int y1) = (Clamp(entry.X + entry.Width - 1, width), Clamp(entry.Y + entry.Depth - 1, depth));
        return new End(new GridRectangle(x0, y0, x1 - x0 + 1, y1 - y0 + 1), Outside: false, Leaves: true);
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

        List<State>? states = Levelled(way, from, to) ?? Search(from, to, pipe, levelled: true);
        return states is null ? null : Lay(states, pipe);
    }

    /// <summary>
    /// Where another belt may join one laid, as an end: its squares but its ends, where it's alone, keeps its level and
    /// has a side free; at its level, from one of those sides. Anywhere along it (2026-11-18: only near its end, short
    /// or crowded belts had no room, and the join fell back to the factory): the search takes the nearest. Null when
    /// there's none.
    /// </summary>
    /// <param name="after">Only squares after this one along the belt, by its place on it: after a merger onto it.</param>
    public End? JointsOn((List<int> Squares, List<int> Levels) belt, int after = 0)
    {
        (List<int> squares, List<int> crossings) = belt;
        Dictionary<int, Joint> joints = [];
        int last = squares.Count - 1;
        for (int i = last - 1; i >= Math.Max(1, after + 1); i--)
        {
            int square = squares[i];
            if (stacked[square] != 1 || crossings[i - 1] != crossings[i])
            {
                continue;
            }

            (int into, int outOf) = (SideTowards(square, squares[i - 1]), SideTowards(square, squares[i + 1]));
            List<int> sides = [.. Enumerable.Range(0, 4).Where(side => side != into && side != outOf && IsInside(square, side))];
            if (sides.Count > 0)
            {
                joints[square] = new Joint(crossings[i], sides);
            }
        }

        if (joints.Count == 0)
        {
            return null;
        }

        int x0 = joints.Keys.Min(square => square % width);
        int y0 = joints.Keys.Min(square => square / width);
        int x1 = joints.Keys.Max(square => square % width);
        int y1 = joints.Keys.Max(square => square / width);
        return new End(new GridRectangle(x0, y0, x1 - x0 + 1, y1 - y0 + 1), Outside: false, joints);
    }

    /// <summary>The side of a square that faces a neighbouring one.</summary>
    private int SideTowards(int square, int neighbour) =>
        (neighbour - square) switch
        {
            var d when d == -width => 0,
            1 => 1,
            var d when d == width => 2,
            _ => 3,
        };

    /// <summary>Whether a square has a neighbour on that side, on the floor.</summary>
    private bool IsInside(int square, int side)
    {
        (int x, int y) = (square % width, square / width);
        return side switch
        {
            0 => y > 0,
            1 => x < width - 1,
            2 => y < depth - 1,
            _ => x > 0,
        };
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
                if (levelled && JointAt(from, square) is { } joint && level != joint.Level)
                {
                    continue;
                }

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
                if (side == state.Side || (state.Side == Lift && JointAt(from, state.Square) is { } leaving && !leaving.Sides.Contains(side)))
                {
                    continue;
                }

                (int nx, int ny) = (x + Offsets[side].X, y + Offsets[side].Y);
                if (nx < 0 || ny < 0 || nx >= width || ny >= depth)
                {
                    continue;
                }

                int next = (ny * width) + nx;
                Joint? joining = JointAt(to, next);
                bool arrives = IsEnd(next, to) && (joining is null || joining.Sides.Contains(Opposite(side)));

                // Another belt's square where this one may join it is only ever an end.
                bool passes = joining is null && JointAt(from, next) is null;
                bool turns = state.Side != Lift && side != Opposite(state.Side);

                // Arriving at the edge for an end off the floor, the way still to go from there counts too.
                long beyond = to.Outside ? Step * DistanceTo(next, to.Space) : 0;

                int slots = (Across(state.Square, side)?.Count ?? 0) + 1;
                for (int slot = 0; slot < slots; slot++)
                {
                    int crossed = state.Side == Lift ? 0 : Crossings(state.Square, state.Side, state.Slot, side, slot, pipe);
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
                        if (arrives && (!levelled || joining is null || level == joining.Level))
                        {
                            Reach(new State(next, Opposite(side), slot, level, Ends: true), state, here + step + beyond, 0);
                        }

                        if (passes && (levelled ? !taken[(next * levels) + level] : HasFreeLevel(next)))
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
    private List<State>? Levelled(List<State> way, End from, End to)
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
                if (!Open(run, level)
                    || (run == 0 && JointAt(from, way[0].Square) is { } leaving && level != leaving.Level)
                    || (run == runs - 1 && JointAt(to, way[last].Square) is { } joining && level != joining.Level))
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
        pipes.Add(pipe);
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
        }

        return (squares, crossings);
    }

    /// <summary>
    /// How many belts a belt crosses that comes into a square by one side at a place in its order, and leaves by another
    /// at a place in that one's; -1 when it may not go so. Walking round the square, every belt shows twice; two that
    /// interleave cross. Only a belt running straight through may cross, and only belts running straight across it: on
    /// another level, which the levels taken on the square see to (decided 2026-11-18). A belt that ends on the square, at
    /// its lift, shows only once, so nothing may pass it: that's what keeps every other belt off a lift. A pipe and a belt
    /// share a square only so, crossing: they never stack together (decided 2026-11-18).
    /// </summary>
    private int Crossings(int square, int inSide, int inSlot, int outSide, int outSlot, bool pipe)
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
                // Beside or in a stack with it: only of one kind.
                if (pipes[belt.Key] != pipe)
                {
                    return -1;
                }

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

    /// <summary>
    /// Where a belt may end: a free square of the end, or of the floor's edge for an end off the floor; or a square of
    /// another belt it joins.
    /// </summary>
    private bool IsEnd(int square, End end) =>
        end.Joints is { } joints ? joints.ContainsKey(square) : Free(square) && (end.Outside ? OnEdge(square) : Within(square, end.Space));

    private static Joint? JointAt(End end, int square) => end.Joints?.GetValueOrDefault(square);

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

/// <summary>
/// Where a belt may start or end on the board: under a factory or at a spot; or off the floor, reached at its edge; or on
/// another belt, at the squares it may join it (<see cref="Joints"/>, within <see cref="Space"/>).
/// </summary>
/// <param name="Leaves">Whether a belt there leaves the floor, to join an existing belt that comes in there.</param>
file sealed record End(GridRectangle Space, bool Outside, IReadOnlyDictionary<int, Joint>? Joints = null, bool Leaves = false);

/// <summary>A square of a belt laid where another may join it: at that belt's level, by one of the sides it doesn't use.</summary>
file sealed record Joint(int Level, IReadOnlyList<int> Sides);

