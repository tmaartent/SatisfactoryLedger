namespace SatisfactoryLedger.World.Domain.Arrangement;

/// <summary>
/// Extractors feeding a factory one item share belts, each filled up to the top belt's (or pipe's) capacity (decided
/// 2026-11-18: limestone from miners of 300, 300 and 600 a minute is one belt of 1200, not three).
/// </summary>
public static class SupplyBelts
{
    /// <summary>
    /// The extractors on each belt: a belt starts with the biggest left, then takes the nearest to it that still fit,
    /// so extractors that stand together share one.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<Supply>> Merge(IEnumerable<Supply> supplies, double capacity)
    {
        ArgumentNullException.ThrowIfNull(supplies);
        List<Supply> left = [.. supplies.OrderByDescending(supply => supply.PerMinute).ThenBy(supply => supply.Id, StringComparer.Ordinal)];
        List<IReadOnlyList<Supply>> belts = [];
        while (left.Count > 0)
        {
            Supply first = left[0];
            left.RemoveAt(0);
            List<Supply> belt = [first];
            double carried = first.PerMinute;
            foreach (Supply next in left.OrderBy(supply => Math.Abs(supply.X - first.X) + Math.Abs(supply.Y - first.Y)).ToList())
            {
                if (carried + next.PerMinute <= capacity)
                {
                    belt.Add(next);
                    carried += next.PerMinute;
                    left.Remove(next);
                }
            }

            belts.Add(belt);
        }

        return belts;
    }

    /// <summary>
    /// What each belt (or pipe) of an item carries when it needs more than one: filled one after the other, the last
    /// with what's left; 1500 a minute on belts of 1200 is 1200 and 300.
    /// </summary>
    public static IReadOnlyList<double> Loads(double perMinute, double capacity)
    {
        List<double> loads = [];
        for (double left = perMinute; left > 0; left -= capacity)
        {
            loads.Add(Math.Min(left, capacity));
        }

        return loads;
    }
}
