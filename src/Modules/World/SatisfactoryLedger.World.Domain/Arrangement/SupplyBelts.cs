namespace SatisfactoryLedger.World.Domain.Arrangement;

/// <summary>
/// Extractors feeding a factory one item share belts, each filled up to the top belt's (or pipe's) capacity (decided
/// 2026-11-18: limestone from miners of 300, 300 and 600 a minute is one belt of 1200, not three).
/// </summary>
public static class SupplyBelts
{
    /// <summary>
    /// How far from the first on a belt another may stand to share it, in centimetres along x and y (decided 2026-11-19):
    /// 50 metres. Buildings further apart keep belts of their own, so each belt starts where its buildings stand.
    /// </summary>
    private const double TogetherCentimetres = 5000;

    /// <summary>
    /// The extractors on each belt: a belt starts with the biggest left, then takes the nearest to it that still fit
    /// and stand together with it (within 50 metres), so only extractors that stand together share one.
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
            double Distance(Supply supply) => Math.Abs(supply.X - first.X) + Math.Abs(supply.Y - first.Y);
            foreach (Supply next in left.Where(supply => Distance(supply) <= TogetherCentimetres).OrderBy(Distance).ToList())
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
