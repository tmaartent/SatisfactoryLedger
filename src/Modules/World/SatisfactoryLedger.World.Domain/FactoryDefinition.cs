namespace SatisfactoryLedger.World.Domain;

/// <summary>
/// A factory as the player defines it: its input items and the output belt where its merged output leaves (BR-W2).
/// The game has no notion of a factory, so this is the player's own data. Its members are derived from the world.
/// </summary>
public sealed class FactoryDefinition
{
    public FactoryDefinition(string name, BuildableId outputBelt, IEnumerable<ItemId> inputItems)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(outputBelt);

        Name = name;
        OutputBelt = outputBelt;
        InputItems = inputItems.ToHashSet();
    }

    public string Name { get; }

    public BuildableId OutputBelt { get; }

    public IReadOnlySet<ItemId> InputItems { get; }

    /// <summary>Finds the factory's machines and logistics in a world (BR-W3, BR-W4).</summary>
    public FactoryMembers TraceMembers(WorldGraph world)
    {
        Buildable output = world.Find(OutputBelt)
            ?? throw new ArgumentException($"The output belt {OutputBelt} is not in the world", nameof(world));
        if (output is not Logistics { IsBelt: true })
        {
            throw new ArgumentException($"{output} is not a belt, so it can't be the output belt of {Name}", nameof(world));
        }

        UpstreamTrace trace = TraceUpstream(world);
        return new FactoryMembers(trace.Machines, trace.FactoryLogistics(world));
    }

    /// <summary>
    /// BR-W3: from the output belt upstream, through logistics and machines. The trace stops at a provider, and at a
    /// machine that makes one of the declared input items, because that machine belongs to another factory (BR-W6).
    /// </summary>
    private UpstreamTrace TraceUpstream(WorldGraph world)
    {
        HashSet<BuildableId> machines = [];
        HashSet<BuildableId> passedLogistics = [OutputBelt];
        Queue<BuildableId> toVisit = new([OutputBelt]);

        while (toVisit.TryDequeue(out BuildableId? current))
        {
            foreach (Buildable upstream in world.Upstream(current))
            {
                bool isNew = upstream switch
                {
                    Machine machine when !machine.ProducesAnyOf(InputItems) => machines.Add(machine.Id),
                    Logistics => passedLogistics.Add(upstream.Id),
                    _ => false,
                };

                if (isNew)
                {
                    toVisit.Enqueue(upstream.Id);
                }
            }
        }

        return new UpstreamTrace(machines, passedLogistics);
    }

    /// <summary>
    /// What the walk upstream from the output belt found: the factory's machines, and every piece of logistics it
    /// passed. Not all of those are the factory's: the walk also passes the supply on the input lines.
    /// </summary>
    private sealed record UpstreamTrace(HashSet<BuildableId> Machines, HashSet<BuildableId> PassedLogistics)
    {
        /// <summary>
        /// Of the logistics the walk passed, the factory's are:
        /// <list type="bullet">
        /// <item>everything fed by the factory's machines, up to and including the output belt;</item>
        /// <item>on the input lines, everything from the first splitter that only feeds the factory (BR-W4).
        /// What lies upstream of that splitter is supply.</item>
        /// </list>
        /// </summary>
        public HashSet<BuildableId> FactoryLogistics(WorldGraph world)
        {
            HashSet<BuildableId> fedByMachines = ReachableDownstream(world, Machines, PassedLogistics);

            // Input side: drop what also feeds something outside the factory, until nothing changes.
            HashSet<BuildableId> inputSide = PassedLogistics.Except(fedByMachines).ToHashSet();
            bool InFactory(BuildableId id) => Machines.Contains(id) || fedByMachines.Contains(id) || inputSide.Contains(id);
            while (inputSide.RemoveWhere(id => !world.Downstream(id).All(next => InFactory(next.Id))) > 0)
            {
            }

            // An input starts at a splitter: what lies between the supply and the first splitter is not the factory's.
            List<BuildableId> inputSplitters = inputSide
                .Where(id => world.Find(id) is Logistics { Kind: LogisticsKind.Splitter })
                .ToList();
            HashSet<BuildableId> logistics = ReachableDownstream(world, inputSplitters, inputSide);
            logistics.UnionWith(inputSplitters);

            logistics.UnionWith(fedByMachines);
            return logistics;
        }

        /// <summary>Everything in <paramref name="within"/> that can be reached downstream from <paramref name="from"/>.</summary>
        private static HashSet<BuildableId> ReachableDownstream(
            WorldGraph world, IEnumerable<BuildableId> from, HashSet<BuildableId> within)
        {
            HashSet<BuildableId> reached = [];
            Queue<BuildableId> toVisit = new(from);

            while (toVisit.TryDequeue(out BuildableId? current))
            {
                foreach (Buildable next in world.Downstream(current))
                {
                    if (within.Contains(next.Id) && reached.Add(next.Id))
                    {
                        toVisit.Enqueue(next.Id);
                    }
                }
            }

            return reached;
        }
    }
}
