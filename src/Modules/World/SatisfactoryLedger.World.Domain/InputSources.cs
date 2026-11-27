using SatisfactoryLedger.World.Domain.Tracing;

namespace SatisfactoryLedger.World.Domain;

/// <summary>
/// Where what arrives over a factory's input comes from (decided 2026-11-11), the mirror of
/// <see cref="OutputDestinations"/>. Its line is followed upstream through logistics to its starts: the machines and
/// providers that put the item on it, a container nothing feeds, or a belt that starts at nothing. Where lines merged,
/// what arrives came from each in proportion to what it brings.
/// </summary>
public static class InputSources
{
    /// <summary>
    /// How much of the <paramref name="perMinute"/> of <paramref name="item"/> arriving at <paramref name="to"/> comes
    /// from each start of its line. A line that only leads back to where the walk passed (a loop) brings nothing of its
    /// own: its share goes to the others.
    /// </summary>
    /// <param name="readings">The analysis: each branch weighs what it sends on of the item (<see cref="FlowReadings.OutOf"/>).</param>
    public static IReadOnlyDictionary<BuildableId, double> Of(
        WorldGraph world, BuildableId to, ItemId item, double perMinute, FlowReadings readings)
    {
        return Fractions(to, []).ToDictionary(start => start.Key, start => start.Value * perMinute);

        // What part of what arrives at a buildable comes from each start: parts that add up to 1, or none for a loop.
        Dictionary<BuildableId, double> Fractions(BuildableId at, HashSet<BuildableId> passed)
        {
            if (world.Find(at) is not Logistics)
            {
                return new() { [at] = 1 };
            }

            if (!world.Upstream(at).Any())
            {
                return new() { [at] = 1 };
            }

            HashSet<BuildableId> here = [.. passed, at];
            List<(Dictionary<BuildableId, double> Parts, double Weight)> branches = [];
            foreach (BuildableId from in world.Upstream(at).Select(from => from.Id).Distinct().Where(from => !here.Contains(from)))
            {
                Dictionary<BuildableId, double> parts = Fractions(from, here);
                if (parts.Count > 0)
                {
                    branches.Add((parts, readings.OutOf(from, item)));
                }
            }

            double total = branches.Sum(branch => branch.Weight);
            Dictionary<BuildableId, double> fractions = [];
            foreach ((Dictionary<BuildableId, double> parts, double weight) in branches)
            {
                double share = total > 0 ? weight / total : 1.0 / branches.Count;
                foreach ((BuildableId start, double part) in parts)
                {
                    fractions[start] = fractions.GetValueOrDefault(start) + part * share;
                }
            }

            return fractions;
        }
    }
}
