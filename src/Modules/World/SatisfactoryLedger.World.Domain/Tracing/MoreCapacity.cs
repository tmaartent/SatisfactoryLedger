namespace SatisfactoryLedger.World.Domain.Tracing;

/// <summary>
/// What a bottleneck needs to carry what's offered to it (decided 2026-11-22): <see cref="Lines"/> side by side of
/// <see cref="Tier"/>, to carry <see cref="Carries"/> per minute. The lowest tier of its kind that carries it alone; when
/// even the highest tier can't, the highest in as many lines as it takes.
/// </summary>
public sealed record MoreCapacity(string Tier, int Lines, double Carries)
{
    /// <param name="conduit">The belt, lift or pipe at its capacity (BR-A12).</param>
    /// <param name="offeredMore">What's offered to it on top of what it carries, per minute.</param>
    /// <param name="tiers">The game's tiers; null is returned when none is of the conduit's kind.</param>
    public static MoreCapacity? For(Logistics conduit, double offeredMore, IEnumerable<TransportTier> tiers)
    {
        LogisticsKind kind = conduit.Kind == LogisticsKind.PipeNetwork ? LogisticsKind.Pipe : conduit.Kind;
        double carries = Math.Round((conduit.Capacity?.ToDouble() ?? 0) + offeredMore, 6);

        // By capacity, then class name: the pipeline with indicators sorts before its "NoIndicator" twin.
        List<TransportTier> ofItsKind = [.. tiers.Where(tier => tier.Kind == kind).OrderBy(tier => tier.Capacity).ThenBy(tier => tier.Id, StringComparer.Ordinal)];
        if (ofItsKind.Count == 0)
        {
            return null;
        }

        TransportTier? enough = ofItsKind.Find(tier => tier.Capacity >= carries - TraceStarts.Tolerance);
        if (enough is not null)
        {
            return new MoreCapacity(enough.Id, 1, carries);
        }

        TransportTier highest = ofItsKind.First(tier => tier.Capacity == ofItsKind[^1].Capacity);
        return new MoreCapacity(highest.Id, (int)Math.Ceiling((carries - TraceStarts.Tolerance) / highest.Capacity), carries);
    }
}
