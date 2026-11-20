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

    public void Add(TraceStart start)
    {
        string key = $"{start.Cause}|{start.Item.Id}|{string.Join(",", start.At.Select(id => id.Id).Order(StringComparer.Ordinal))}";
        if (_keys.Add(key))
        {
            _starts.Add(start);
        }
    }

    /// <summary>What a belt, lift or pipe carries at most per minute; 0 when it has no limit known.</summary>
    public static double Capacity(WorldGraph world, BuildableId id) => world.Find(id) is Logistics { Capacity: { } capacity } ? capacity.ToDouble() : 0;

    /// <summary>Whether something is more than rounding, compared with what it's part of.</summary>
    public static bool Matters(double amount, double of) => amount > Math.Max(Tolerance, of * 0.01);

    public static IReadOnlyDictionary<BuildableId, double> Rounded(Dictionary<BuildableId, double> amounts) =>
        amounts.Where(entry => entry.Value > Tolerance).ToDictionary(entry => entry.Key, entry => Math.Round(entry.Value, 6));
}
