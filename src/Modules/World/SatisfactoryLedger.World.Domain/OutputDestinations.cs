using SatisfactoryLedger.SharedKernel;

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
    /// <param name="flowInto">What flows into a buildable per minute, of this item, as the analysis found it.</param>
    public static IReadOnlyDictionary<BuildableId, double> Of(
        WorldGraph world, BuildableId from, ItemId item, double perMinute, Func<BuildableId, double> flowInto)
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

            double total = onwards.Sum(flowInto);
            foreach (BuildableId next in onwards)
            {
                double share = total > 0 ? flowInto(next) / total : 1.0 / onwards.Count;
                Follow(next, amount * share, here);
            }
        }

        void Reach(BuildableId end, double amount) => ends[end] = ends.GetValueOrDefault(end) + amount;
    }

    /// <summary>
    /// What the machines and generators the lines from <paramref name="from"/> reach would take of
    /// <paramref name="item"/> at 100%: a machine what its recipe asks for at its clock speed, a generator its fuel or
    /// water. Whatever else feeds them, and each once, however many of the lines reach it. Sinks, stations and
    /// containers ask for nothing in particular.
    /// </summary>
    public static Rate ReceiversNeed(WorldGraph world, IEnumerable<BuildableId> from, ItemId item) =>
        Receivers(world, from).Aggregate(Rate.Zero, (need, receiver) => need + Asks(receiver, item));

    /// <summary>
    /// What those same receivers, the ones that ask for <paramref name="item"/>, take of it now, from whatever source:
    /// the sum of <paramref name="flowInto"/> over them, each once (decided 2026-11-11).
    /// </summary>
    public static double ReceiversGet(WorldGraph world, IEnumerable<BuildableId> from, ItemId item, Func<BuildableId, double> flowInto) =>
        Receivers(world, from).Where(receiver => Asks(receiver, item) > Rate.Zero).Sum(receiver => flowInto(receiver.Id));

    /// <summary>What a receiver asks for of an item at 100%: a machine its recipe at its clock speed, a generator its fuel or water.</summary>
    private static Rate Asks(Buildable receiver, ItemId item) => receiver switch
    {
        Machine machine => machine.Demand.GetValueOrDefault(item) ?? Rate.Zero,
        Consumer { Generation: { } generation } when generation.Fuel == item => generation.Burns,
        Consumer { Generation: { Supplemental: { } water } generation } when water == item => generation.SupplementalNeeded,
        _ => Rate.Zero,
    };

    /// <summary>The machines and consumers the lines from <paramref name="from"/> reach through logistics, each once.</summary>
    private static IEnumerable<Buildable> Receivers(WorldGraph world, IEnumerable<BuildableId> from)
    {
        HashSet<BuildableId> seen = [.. from];
        Queue<BuildableId> next = new(seen);
        while (next.TryDequeue(out BuildableId? at))
        {
            Buildable? buildable = world.Find(at);
            if (buildable is not Logistics)
            {
                if (buildable is not null)
                {
                    yield return buildable;
                }

                continue;
            }

            foreach (Buildable onwards in world.Downstream(at).Where(onwards => seen.Add(onwards.Id)))
            {
                next.Enqueue(onwards.Id);
            }
        }
    }
}
