namespace SatisfactoryLedger.World.Domain.Tracing;

/// <summary>
/// The starts a trace finds, each once: the same cause of the same item at the same buildables, reached along another
/// path, is the same start. The first path found is kept.
/// </summary>
internal sealed class TraceStarts
{
    /// <summary>A trace this deep has gone round something it can't see the end of: it stops there.</summary>
    public const int MostSteps = 12;

    /// <summary>Less than this per minute, or than this part of what's sent, is rounding, not a flow.</summary>
    public const double Tolerance = 1e-3;

    private readonly List<TraceStart> _starts = [];
    private readonly HashSet<string> _keys = new(StringComparer.Ordinal);

    public IReadOnlyList<TraceStart> All => _starts;

    /// <summary>How many starts were found, counting one found again along another path: whether a follow ended anywhere.</summary>
    public int Found { get; private set; }

    public void Add(TraceStart start)
    {
        Found++;
        string key = $"{start.Cause}|{start.Item.Id}|{string.Join(",", start.At.Select(id => id.Id).Order(StringComparer.Ordinal))}";
        if (_keys.Add(key))
        {
            _starts.Add(start);
        }
    }

    /// <summary>
    /// A belt, lift or pipe at its capacity (BR-A12) as a start: what it carries at most per minute, and what it takes
    /// to carry what's offered to it (decided 2026-11-22).
    /// </summary>
    public static TraceStart Bottleneck(
        WorldGraph world, FlowReadings readings, BuildableId conduit, ItemId item, IReadOnlyList<TraceStep> path, IReadOnlyCollection<TransportTier> tiers)
    {
        Logistics? logistics = world.Find(conduit) as Logistics;
        return new TraceStart(TraceCause.BeltFull, item, [conduit], path, logistics?.Capacity?.ToDouble() ?? 0)
        {
            MoreCapacity = logistics is null ? null : MoreCapacity.For(logistics, readings.OfferedMore(conduit), tiers),
        };
    }

    /// <summary>Whether something is more than rounding, compared with what it's part of.</summary>
    public static bool Matters(double amount, double of) => amount > Math.Max(Tolerance, of * 0.01);

    public static IReadOnlyDictionary<BuildableId, double> Rounded(Dictionary<BuildableId, double> amounts) =>
        amounts.Where(entry => entry.Value > Tolerance).ToDictionary(entry => entry.Key, entry => Math.Round(entry.Value, 6));
}
