using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Arrangement;

/// <summary>
/// The belts that exist in the save between a building outside the arranged area and a factory inside it (decided
/// 2026-11-18): a belt the router lays to that building leaves the floor where the existing one crosses into the area,
/// and outside it the existing one stays. Followed along belts, lifts, splitters, mergers and containers, downstream from
/// a building that supplies and upstream to one that's supplied. Pipes are followed piece by piece, along the pipes
/// that touch (decided 2026-11-18), since the model joins them into one network with no place of its own.
/// </summary>
internal static class ExistingWays
{
    /// <param name="pipes">Per pipe, the pipes it touches (<see cref="SnapshotDocument.PipeNeighbours"/>).</param>
    /// <param name="building">The building outside the area: a node's extractor, a station or a drone port.</param>
    /// <param name="machines">The arranged factories' machines: the way ends at the first it reaches.</param>
    /// <returns>Where its way crosses into the area, and the way outside it from the building; null when there's none.</returns>
    public static ExistingWay? Into(
        WorldGraph world,
        IReadOnlyDictionary<string, BuildableDocument> buildables,
        ILookup<string, string> pipes,
        string building,
        IReadOnlySet<string> machines,
        WorldRectangle area)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(buildables);
        ArgumentNullException.ThrowIfNull(pipes);
        ArgumentNullException.ThrowIfNull(machines);

        // Along the connections one way, and along the pipes either way.
        IEnumerable<Buildable> Down(BuildableId id) => world.Downstream(id).Concat(Touching(id));
        IEnumerable<Buildable> Up(BuildableId id) => world.Upstream(id).Concat(Touching(id));
        IEnumerable<Buildable> Touching(BuildableId id) => pipes[id.Id].Select(other => world.Find(BuildableId.Of(other))).OfType<Buildable>();
        List<string>? way = Search(buildables, building, machines, Down);
        if (way is null && Search(buildables, building, machines, Up) is { } back)
        {
            way = back;
        }

        return way is null ? null : Entering(PointsOf(way, buildables), area);
    }

    /// <summary>Where a way, from outside, first crosses into an area, and the way up to there; null when it never does.</summary>
    public static ExistingWay? Entering(IReadOnlyList<WorldPoint> way, WorldRectangle area)
    {
        ArgumentNullException.ThrowIfNull(way);
        ArgumentNullException.ThrowIfNull(area);
        if (way.Count == 0 || Inside(way[0], area))
        {
            return null;
        }

        for (int i = 1; i < way.Count; i++)
        {
            if (Inside(way[i], area))
            {
                WorldPoint entry = Crossing(way[i - 1], way[i], area);
                return new ExistingWay(entry, [.. way.Take(i), entry]);
            }
        }

        return null;
    }

    /// <summary>
    /// The shortest way from one building to any of the machines, through logistics only, ends included: by the distance
    /// between the buildings' middles, not their number (2026-11-18: a detour of a few long belts beat a direct way of
    /// many pieces).
    /// </summary>
    private static List<string>? Search(
        IReadOnlyDictionary<string, BuildableDocument> buildables, string from, IReadOnlySet<string> machines, Func<BuildableId, IEnumerable<Buildable>> next)
    {
        Dictionary<string, string?> before = new(StringComparer.Ordinal) { [from] = null };
        Dictionary<string, double> cost = new(StringComparer.Ordinal) { [from] = 0 };
        PriorityQueue<string, double> open = new([(from, 0.0)]);
        HashSet<string> done = new(StringComparer.Ordinal);
        while (open.TryDequeue(out string? at, out double here))
        {
            if (!done.Add(at))
            {
                continue;
            }

            if (machines.Contains(at))
            {
                List<string> way = [];
                for (string? step = at; step is not null; step = before[step])
                {
                    way.Add(step);
                }

                way.Reverse();
                return way;
            }

            if (at != from && buildables.GetValueOrDefault(at) is { Role: BuildableDocumentRole.Machine })
            {
                continue;
            }

            foreach (Buildable neighbour in next(BuildableId.Of(at)))
            {
                string id = neighbour.Id.Id;
                if (!machines.Contains(id) && neighbour is not Logistics { Kind: not LogisticsKind.PipeNetwork })
                {
                    continue;
                }

                double total = here + Apart(Middle(buildables, at), Middle(buildables, id));
                if (total < cost.GetValueOrDefault(id, double.MaxValue))
                {
                    (cost[id], before[id]) = (total, at);
                    open.Enqueue(id, total);
                }
            }
        }

        return null;
    }

    /// <summary>Where a building's middle is, seen from above: its curve's middle point, or where it stands; none without a place.</summary>
    private static WorldPoint? Middle(IReadOnlyDictionary<string, BuildableDocument> buildables, string id) =>
        buildables.GetValueOrDefault(id) switch
        {
            { Curve: { Count: > 0 } curve } => new WorldPoint(curve[curve.Count / 2].Location.X, curve[curve.Count / 2].Location.Y, curve[curve.Count / 2].Location.Z),
            { Position: { } position } => new WorldPoint(position.X, position.Y, position.Z),
            _ => null,
        };

    private static double Apart(WorldPoint? a, WorldPoint? b) => a is null || b is null ? 0 : Distance(a, b);

    /// <summary>
    /// A way's buildings as points seen from above: a belt's curve, sampled along its tangents as the game lays it (not
    /// straight from point to point: 2026-11-19, drawn as zigzags), each turned to run on from the point before, or else
    /// where a building stands. One without a place is left out. It starts where the building's first belt does, by its
    /// output, rather than at the building's middle (2026-11-18: a short diagonal from each freight platform's middle);
    /// the middle only turns that belt the right way round, or starts the way when the belt has no curve.
    /// </summary>
    private static List<WorldPoint> PointsOf(List<string> way, IReadOnlyDictionary<string, BuildableDocument> buildables)
    {
        List<BuildableDocument> buildings = [.. way.Select(buildables.GetValueOrDefault).OfType<BuildableDocument>()];
        List<WorldPoint> points = [];
        WorldPoint? before = null;
        foreach ((BuildableDocument building, int index) in buildings.Select((building, index) => (building, index)))
        {
            List<WorldPoint> along = building.Curve is { Count: > 0 } curve
                ? Sampled(curve)
                : building.Position is { } position ? [new WorldPoint(position.X, position.Y, position.Z)] : [];
            if ((points.Count > 0 ? points[^1] : before) is { } last && along.Count > 1 && Distance(last, along[^1]) < Distance(last, along[0]))
            {
                along.Reverse();
            }

            if (index == 0 && buildings.Count > 1 && buildings[1].Curve is { Count: > 0 } && along.Count == 1)
            {
                before = along[0];
                continue;
            }

            points.AddRange(along);
        }

        return points;
    }

    /// <summary>Points per stretch of a belt between two of its points, as the map draws it.</summary>
    private const int StepsPerStretch = 6;

    /// <summary>
    /// A belt's curve: a cubic Hermite spline from each point to the next, leaving along the one's leave tangent and
    /// arriving along the other's arrive tangent, as the game lays it and the map draws it; heights included, to draw it in
    /// 3D as built.
    /// </summary>
    private static List<WorldPoint> Sampled(IReadOnlyList<CurvePointDocument> curve)
    {
        List<WorldPoint> points = [new WorldPoint(curve[0].Location.X, curve[0].Location.Y, curve[0].Location.Z)];
        for (int i = 0; i + 1 < curve.Count; i++)
        {
            (PositionDocument from, PositionDocument to) = (curve[i].Location, curve[i + 1].Location);
            (PositionDocument leave, PositionDocument arrive) = (curve[i].LeaveTangent, curve[i + 1].ArriveTangent);
            for (int step = 1; step <= StepsPerStretch; step++)
            {
                double t = (double)step / StepsPerStretch;
                (double h00, double h10, double h01, double h11) =
                    ((2 * t * t * t) - (3 * t * t) + 1, (t * t * t) - (2 * t * t) + t, (-2 * t * t * t) + (3 * t * t), (t * t * t) - (t * t));
                points.Add(new WorldPoint(
                    (h00 * from.X) + (h10 * leave.X) + (h01 * to.X) + (h11 * arrive.X),
                    (h00 * from.Y) + (h10 * leave.Y) + (h01 * to.Y) + (h11 * arrive.Y),
                    (h00 * from.Z) + (h10 * leave.Z) + (h01 * to.Z) + (h11 * arrive.Z)));
            }
        }

        return points;
    }

    private static double Distance(WorldPoint a, WorldPoint b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    private static bool Inside(WorldPoint point, WorldRectangle area) =>
        point.X >= area.X0 && point.X <= area.X1 && point.Y >= area.Y0 && point.Y <= area.Y1;

    /// <summary>Where the stretch from a point outside to one inside crosses the area's edge.</summary>
    private static WorldPoint Crossing(WorldPoint outside, WorldPoint inside, WorldRectangle area)
    {
        // The latest of the edges it passes on its way in (Liang–Barsky, entering side only).
        (double dx, double dy) = (inside.X - outside.X, inside.Y - outside.Y);
        double t = 0;
        foreach ((double delta, double distance) in new[] { (dx, area.X0 - outside.X), (-dx, outside.X - area.X1), (dy, area.Y0 - outside.Y), (-dy, outside.Y - area.Y1) })
        {
            if (delta > 0 && distance > 0)
            {
                t = Math.Max(t, distance / delta);
            }
        }

        // The height in between, where both ends have one.
        double? z = outside.Z is { } from && inside.Z is { } to ? from + (t * (to - from)) : null;
        return new WorldPoint(outside.X + (t * dx), outside.Y + (t * dy), z);
    }
}
