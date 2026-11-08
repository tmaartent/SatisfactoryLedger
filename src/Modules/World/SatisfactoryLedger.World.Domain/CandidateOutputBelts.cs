namespace SatisfactoryLedger.World.Domain;

/// <summary>Belts where items leave a group of machines: suggestions for a factory's output belt (BR-W12).</summary>
public static class CandidateOutputBelts
{
    public static IReadOnlyList<OutputBeltCandidate> In(WorldGraph world)
    {
        List<OutputBeltCandidate> candidates = [];

        foreach (Logistics belt in world.Buildables.OfType<Logistics>().Where(logistics => logistics.IsBelt))
        {
            List<Buildable> into = [.. world.Downstream(belt.Id)];
            if (!into.All(EndsHere))
            {
                continue;
            }

            List<Machine> feeding = MachinesFeeding(world, belt.Id);
            if (feeding.Count > 0)
            {
                HashSet<ItemId> items = [.. feeding.SelectMany(machine => machine.Produces)];
                candidates.Add(new OutputBeltCandidate(belt.Id, items, [.. into.Select(buildable => buildable.Id)]));
            }
        }

        return [.. candidates.OrderBy(candidate => candidate.Belt.Id, StringComparer.Ordinal)];
    }

    private static bool EndsHere(Buildable next) => next is Consumer or Logistics { Kind: LogisticsKind.StorageContainer };

    /// <summary>The machines that feed a belt through logistics only: walking upstream stops at the first machine.</summary>
    private static List<Machine> MachinesFeeding(WorldGraph world, BuildableId belt)
    {
        List<Machine> machines = [];
        HashSet<BuildableId> visited = [belt];
        Queue<BuildableId> toVisit = new([belt]);

        while (toVisit.TryDequeue(out BuildableId? current))
        {
            foreach (Buildable upstream in world.Upstream(current).Where(upstream => visited.Add(upstream.Id)))
            {
                switch (upstream)
                {
                    case Machine machine:
                        machines.Add(machine);
                        break;
                    case Logistics:
                        toVisit.Enqueue(upstream.Id);
                        break;
                }
            }
        }

        return machines;
    }
}