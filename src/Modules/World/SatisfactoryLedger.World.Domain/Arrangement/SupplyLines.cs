namespace SatisfactoryLedger.World.Domain.Arrangement;

/// <summary>
/// Supply lines on the belt floor (decided 2026-11-18, BR-S): what brings an item, from extractors, stations, drone ports
/// or factories, runs as one line past the factories (and stations) that take it, instead of a belt from each source to
/// each. Each takes what it needs (a tap, BR-S3); where the line runs short, a priority merger tops it up from another
/// source (an injection, BR-S4), the line's own flow first.
/// <list type="bullet">
/// <item>A line starts at the largest source left, and runs to the consumer nearest to where it is, one after the
/// other. Past a consumer it runs on from a splitter under it (a manifold, BR-S3; decided 2026-11-19): the leg after
/// leaves the leg into it.</item>
/// <item>It carries no more than the consumers left take: never more than its capacity (BR-S1).</item>
/// <item>Before a consumer it can't feed, it's topped up from the source nearest that consumer, as full as the belt
/// and what's still needed allow, so the consumers after are fed too; then the next nearest, as long as it's short.
/// Pipes have none (BR-S11): the consumer takes what's left.</item>
/// <item>A top-up's overflow runs on (decided 2026-11-18, BR-S5): a smart splitter before the merger sends the merger
/// what it takes and the rest on, so what the source has left is a source where the merger stands. It may top up
/// another merger, start a line of its own, or feed that factory straight from the splitter. Each stretch of the top-up
/// belt carries what's taken after it.</item>
/// <item>A line ends when it's empty; the next starts while consumers are left.</item>
/// <item>A factory never feeds itself its own item; ends not on the floor are left out.</item>
/// </list>
/// </summary>
public static class SupplyLines
{
    /// <summary>Below this, a flow is nothing: rates are approximations (BR-A10).</summary>
    private const double Nothing = 1e-6;

    /// <param name="ends">Where the factories and spots stand on the floor, in squares.</param>
    /// <returns>The legs of every line, line by line from its start; an injection after the leg it merges into. A leg
    /// from a factory to itself, an overflow feeding the factory it stands at, isn't a belt on the floor and isn't
    /// among them.</returns>
    public static IReadOnlyList<SupplyLeg> Plan(ItemSupply supply, IReadOnlyDictionary<string, GridRectangle> ends)
    {
        ArgumentNullException.ThrowIfNull(supply);
        ArgumentNullException.ThrowIfNull(ends);
        List<Source> sources = [.. supply.Sources
            .Where(source => ends.ContainsKey(source.Id) && source.PerMinute > Nothing)
            .Select(source => new Source(source.Id, source.PerMinute, Feeder: null))];
        List<SupplyEnd> consumers = [.. supply.Consumers.Where(consumer => ends.ContainsKey(consumer.Id) && consumer.PerMinute > Nothing)];
        List<Leg> legs = [];

        // Takes from a source: every stretch of the top-up belt that brought it there carries it too.
        void Draw(Source source, double perMinute)
        {
            source.PerMinute -= perMinute;
            if (source.PerMinute <= Nothing)
            {
                sources.Remove(source);
            }

            for (Leg? stretch = source.Feeder; stretch is not null; stretch = stretch.Before)
            {
                stretch.PerMinute += perMinute;
            }
        }

        void Tap(SupplyEnd consumer, double perMinute)
        {
            int index = consumers.IndexOf(consumer);
            if (consumer.PerMinute - perMinute > Nothing)
            {
                consumers[index] = consumer with { PerMinute = consumer.PerMinute - perMinute };
            }
            else
            {
                consumers.RemoveAt(index);
            }
        }

        while (sources.Count > 0 && consumers.Count > 0)
        {
            Source start = sources.OrderByDescending(source => source.PerMinute).ThenBy(source => source.Id, StringComparer.Ordinal).First();
            List<SupplyEnd> fed = [.. consumers.Where(start.MayFeed)];
            double needed = fed.Sum(consumer => consumer.PerMinute);
            if (needed <= Nothing)
            {
                // It only feeds itself.
                sources.Remove(start);
                continue;
            }

            double flow = Math.Min(Math.Min(start.PerMinute, supply.Capacity), needed);
            Draw(start, flow);
            string at = start.Id;
            bool overflow = start.Feeder is not null;

            // Past a factory the line runs on from a splitter under it, on the leg into it (decided 2026-11-19, BR-S3).
            Leg? into = null;
            while (flow > Nothing && Nearest(consumers.Where(start.MayFeed), at, consumer => consumer.Id) is { } next)
            {
                var line = new Leg(at, next.Id, flow, Injection: false, overflow, Before: null) { Off = into ?? (overflow ? start.Feeder : null) };
                legs.Add(line);
                if (line.From != line.To)
                {
                    into = line;
                }

                // Fed straight from the splitter, its own factory takes no belt: the next one leaving it is the overflow.
                overflow &= at == next.Id;
                needed -= next.PerMinute;
                while (!supply.Pipe && flow < next.PerMinute - Nothing && supply.Capacity - flow > Nothing
                    && Nearest(sources.Where(source => source.MayFeed(next)), next.Id, source => source.Id) is { } extra)
                {
                    double drawn = Math.Min(Math.Min(extra.PerMinute, supply.Capacity - flow), next.PerMinute + needed - flow);
                    var topUp = new Leg(extra.Id, next.Id, drawn, Injection: true, Overflow: extra.Feeder is not null, Before: extra.Feeder)
                    {
                        Onto = line,
                        Off = extra.Feeder,
                    };
                    legs.Add(topUp);
                    Draw(extra, drawn);

                    // What it has left runs on from the smart splitter before the merger.
                    if (sources.Remove(extra))
                    {
                        sources.Add(new Source(next.Id, extra.PerMinute, topUp));
                    }

                    flow += drawn;
                }

                double tapped = Math.Min(flow, next.PerMinute);
                Tap(next, tapped);
                needed += next.PerMinute - tapped;
                flow -= tapped;
                at = next.Id;
            }
        }

        List<Leg> belts = [.. legs.Where(leg => leg.From != leg.To)];
        // By reference: two legs alike in every value are still two belts.
        var numbers = new Dictionary<Leg, int>(ReferenceEqualityComparer.Instance);
        foreach ((Leg leg, int number) in belts.Select((leg, number) => (leg, number)))
        {
            numbers[leg] = number;
        }

        int? NumberOf(Leg? leg) => leg is not null && numbers.TryGetValue(leg, out int number) ? number : null;
        return [.. belts.Select(leg => new SupplyLeg(
            leg.From, leg.To, supply.Item, supply.Pipe, leg.PerMinute, leg.Injection, leg.Overflow, NumberOf(leg.Onto), NumberOf(leg.Off)))];

        T? Nearest<T>(IEnumerable<T> candidates, string to, Func<T, string> idOf)
            where T : class => candidates
            .OrderBy(candidate => Distance(ends[idOf(candidate)], ends[to]))
            .ThenBy(idOf, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>How far apart two ends' middles are, along the floor's rows and columns, in half squares.</summary>
    private static long Distance(GridRectangle a, GridRectangle b) =>
        Math.Abs((2L * a.X) + a.Width - (2L * b.X) - b.Width) + Math.Abs((2L * a.Y) + a.Depth - (2L * b.Y) - b.Depth);

    /// <summary>What a line can still draw on, where it stands; and the top-up belt that brought it there, for an overflow.</summary>
    private sealed class Source(string id, double perMinute, Leg? Feeder)
    {
        public string Id { get; } = id;

        public double PerMinute { get; set; } = perMinute;

        public Leg? Feeder { get; } = Feeder;

        /// <summary>A factory never feeds itself its own item; an overflow standing at it may.</summary>
        public bool MayFeed(SupplyEnd consumer) => Feeder is not null || consumer.Id != Id;
    }

    /// <summary>A leg as it's planned: what it carries grows while overflow after it is taken.</summary>
    /// <param name="Overflow">Whether it starts at a smart splitter, carrying a top-up's overflow on.</param>
    /// <param name="Before">The stretch of the top-up belt before it, for a top-up from an overflow.</param>
    private sealed record Leg(string From, string To, double PerMinute, bool Injection, bool Overflow, Leg? Before)
    {
        public double PerMinute { get; set; } = PerMinute;

        /// <summary>For a top-up, the line's leg it merges onto.</summary>
        public Leg? Onto { get; init; }

        /// <summary>For an overflow, the top-up leg whose smart splitter it leaves.</summary>
        public Leg? Off { get; init; }
    }
}
