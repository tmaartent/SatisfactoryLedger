using SatisfactoryLedger.World.Domain.Tracing;

namespace SatisfactoryLedger.World.Domain;

/// <summary>
/// Where a factory's output goes (BR-A18, decided 2026-11-11). Its line is followed through logistics to its ends: the
/// machines and consumers it reaches, a container it ends in, or a belt that goes nowhere. At a split, the output goes on
/// as the flow there does: where other supply merged in before, each side gets the output's share of the mix.
/// </summary>
public static class OutputDestinations
{
    /// <summary>
    /// What reaches each end of the line from <paramref name="from"/>, per minute, of the <paramref name="perMinute"/>
    /// of <paramref name="item"/> that sets off on it. A part that loops back to where it passed isn't followed again.
    /// </summary>
    /// <param name="readings">The analysis: at a split, each side gets what flows into it of the item (<see cref="FlowReadings.Into"/>).</param>
    public static IReadOnlyDictionary<BuildableId, double> Of(
        WorldGraph world, BuildableId from, ItemId item, double perMinute, FlowReadings readings)
    {
        Dictionary<BuildableId, double> ends = [];
        Follow(from, perMinute, []);
        return ends;

        void Follow(BuildableId at, double amount, HashSet<BuildableId> passed)
        {
            if (world.Find(at) is not Logistics)
            {
                Reach(at, amount);
                return;
            }

            HashSet<BuildableId> here = [.. passed, at];
            List<BuildableId> onwards = [.. world.Downstream(at).Select(next => next.Id).Distinct().Where(next => !here.Contains(next))];
            if (onwards.Count == 0)
            {
                // A loop back counts as having gone on; a line that goes nowhere ends here.
                if (!world.Downstream(at).Any())
                {
                    Reach(at, amount);
                }

                return;
            }

            double total = onwards.Sum(next => readings.Into(next, item));
            foreach (BuildableId next in onwards)
            {
                double share = total > 0 ? readings.Into(next, item) / total : 1.0 / onwards.Count;
                Follow(next, amount * share, here);
            }
        }

        void Reach(BuildableId end, double amount) => ends[end] = ends.GetValueOrDefault(end) + amount;
    }
}
