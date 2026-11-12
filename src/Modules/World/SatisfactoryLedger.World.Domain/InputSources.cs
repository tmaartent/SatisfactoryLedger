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
    /// <param name="flowOutOf">What a buildable sends on per minute, of this item, as the analysis found it.</param>
    public static IReadOnlyDictionary<BuildableId, double> Of(
        WorldGraph world, BuildableId to, ItemId item, double perMinute, Func<BuildableId, double> flowOutOf)
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
                    branches.Add((parts, flowOutOf(from)));
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

    /// <summary>
    /// How much more could reach <paramref name="to"/> than does now (decided 2026-11-11): what its sources have to
    /// spare, and what branches off its line into a sink, as far as the room left on the logistics between lets it
    /// through. Sources sharing a line share its room. This is a maximum flow from the sources to the inputs, each piece
    /// of logistics passing at most its room; what flows into a sink now may be sent back up its branch, as far as the
    /// branch carries it, to come to the inputs instead.
    /// </summary>
    /// <param name="spare">What a source could send more per minute: a provider's unused supply, a machine's unused capacity.</param>
    /// <param name="room">What a piece of logistics could carry more per minute: its capacity less what it carries now.</param>
    /// <param name="carries">What a piece of logistics carries of the item now, per minute.</param>
    /// <param name="sunk">What a sink takes of the item now, per minute; nothing for anything else.</param>
    public static double CouldBringMore(
        WorldGraph world,
        IEnumerable<BuildableId> to,
        Func<BuildableId, double> spare,
        Func<BuildableId, double> room,
        Func<BuildableId, double> carries,
        Func<BuildableId, double> sunk)
    {
        // The line upstream of the inputs, as far as its sources.
        List<BuildableId> starts = [.. to.Distinct()];
        List<BuildableId> nodes = [];
        Dictionary<BuildableId, int> index = [];
        HashSet<BuildableId> line = [];
        Queue<BuildableId> toVisit = new(starts);
        while (toVisit.TryDequeue(out BuildableId? at))
        {
            if (Add(at))
            {
                line.Add(at);
                if (IsPassedThrough(at))
                {
                    foreach (Buildable from in world.Upstream(at))
                    {
                        toVisit.Enqueue(from.Id);
                    }
                }
            }
        }

        // The branches off that line, as far as their ends: what goes into a sink there could come instead.
        Queue<BuildableId> branches = new(line.Where(IsPassedThrough).SelectMany(at => world.Downstream(at)).Select(next => next.Id).Where(next => !line.Contains(next)));
        while (branches.TryDequeue(out BuildableId? at))
        {
            if (Add(at) && world.Find(at) is Logistics)
            {
                foreach (Buildable next in world.Downstream(at))
                {
                    branches.Enqueue(next.Id);
                }
            }
        }

        // Each buildable is two points, in and out; logistics passes at most its room from one to the other.
        var flow = new MaximumFlow((nodes.Count * 2) + 2);
        int source = nodes.Count * 2;
        int sink = source + 1;
        foreach (BuildableId node in nodes)
        {
            int i = index[node];
            if (line.Contains(node) && !IsPassedThrough(node))
            {
                flow.Add(source, Out(node), Math.Max(0, spare(node)));
                continue;
            }

            if (world.Find(node) is Logistics)
            {
                // On a branch, what it carries now may go back the way it came.
                flow.Add(In(node), Out(node), Math.Max(0, room(node)), line.Contains(node) ? 0 : Math.Max(0, carries(node)));
            }
            else
            {
                flow.Add(source, In(node), Math.Max(0, sunk(node)));
            }

            foreach (Buildable from in world.Upstream(node).Where(from => index.ContainsKey(from.Id)))
            {
                flow.Add(Out(from.Id), In(node), double.PositiveInfinity, line.Contains(node) ? 0 : double.PositiveInfinity);
            }
        }

        foreach (BuildableId start in starts)
        {
            flow.Add(Out(start), sink, double.PositiveInfinity);
        }

        return flow.From(source, sink);

        bool Add(BuildableId id)
        {
            if (index.ContainsKey(id))
            {
                return false;
            }

            index[id] = nodes.Count;
            nodes.Add(id);
            return true;
        }

        int In(BuildableId id) => 2 * index[id];

        int Out(BuildableId id) => (2 * index[id]) + 1;

        // Logistics fed from upstream passes things on; anything else, or logistics nothing feeds, is a source.
        bool IsPassedThrough(BuildableId id) => world.Find(id) is Logistics && world.Upstream(id).Any();
    }

    /// <summary>A maximum flow by shortest augmenting paths (Edmonds-Karp): the graphs here are a line or two upstream.</summary>
    private sealed class MaximumFlow(int points)
    {
        private readonly List<double> _capacity = [];
        private readonly List<int>[] _out = [.. Enumerable.Range(0, points).Select(_ => new List<int>())];
        private readonly List<int> _to = [];

        /// <summary>An edge, and its way back: what it may carry back before anything has gone forward.</summary>
        public void Add(int from, int to, double capacity, double back = 0)
        {
            _out[from].Add(_to.Count);
            _to.Add(to);
            _capacity.Add(capacity);
            _out[to].Add(_to.Count);
            _to.Add(from);
            _capacity.Add(back);
        }

        public double From(int source, int sink)
        {
            double total = 0;
            while (true)
            {
                int[] via = Enumerable.Repeat(-1, _out.Length).ToArray();
                Queue<int> queue = new([source]);
                via[source] = -2;
                while (queue.TryDequeue(out int at) && via[sink] == -1)
                {
                    foreach (int edge in _out[at].Where(edge => _capacity[edge] > 1e-9 && via[_to[edge]] == -1))
                    {
                        via[_to[edge]] = edge;
                        queue.Enqueue(_to[edge]);
                    }
                }

                if (via[sink] == -1)
                {
                    return total;
                }

                double bottleneck = double.PositiveInfinity;
                for (int at = sink; at != source; at = _to[via[at] ^ 1])
                {
                    bottleneck = Math.Min(bottleneck, _capacity[via[at]]);
                }

                if (double.IsPositiveInfinity(bottleneck))
                {
                    return double.PositiveInfinity;
                }

                for (int at = sink; at != source; at = _to[via[at] ^ 1])
                {
                    _capacity[via[at]] -= bottleneck;
                    _capacity[via[at] ^ 1] += bottleneck;
                }

                total += bottleneck;
            }
        }
    }
}
