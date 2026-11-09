namespace SatisfactoryLedger.World.Domain;

/// <summary>
/// A factory as the player defines it: its machines (BR-W2). The game has no notion of a factory, so this is the
/// player's own data. Its logistics, outputs and inputs are derived from the world (BR-W3, BR-W4).
/// </summary>
public sealed class FactoryDefinition
{
    /// <remarks>Its name is the player's label, kept with the stored definition: tracing doesn't need it.</remarks>
    public FactoryDefinition(IEnumerable<BuildableId> machines)
    {
        Machines = machines.ToHashSet();
        if (Machines.Count == 0)
        {
            throw new ArgumentException("A factory has at least one machine", nameof(machines));
        }
    }

    public IReadOnlySet<BuildableId> Machines { get; }

    /// <summary>Whether any of its machines is in the world (BR-W11).</summary>
    public bool IsTraceableIn(WorldGraph world) => Machines.Any(id => world.Find(id) is Machine);

    /// <summary>Finds what belongs to the factory in a world: its machines there, its logistics, outputs and inputs.</summary>
    public FactoryMembers TraceMembers(WorldGraph world)
    {
        HashSet<BuildableId> machines = Machines.Where(id => world.Find(id) is Machine).ToHashSet();
        HashSet<BuildableId> logistics = OwnedLogistics(world, machines);
        var factory = new Trace(world, machines, logistics);

        List<FactoryConnection> outputs = factory.Outputs();
        List<FactoryConnection> inputs = factory.Inputs();
        return new FactoryMembers(
            machines,
            logistics,
            outputs,
            inputs,
            factory.Suppliers(inputs),
            outputs.SelectMany(output => factory.MachinesReaching(output.Inside)).ToHashSet());
    }

    /// <summary>
    /// BR-W3: the logistics connected to the factory's machines whose every machine, through logistics, upstream or
    /// downstream, is the factory's. Pieces of logistics connected to each other form one line, so this is decided per
    /// line: a line that also touches another factory's machine connects the two and belongs to neither. Providers,
    /// consumers and containers are the ends of a line and don't count as another factory.
    /// </summary>
    private static HashSet<BuildableId> OwnedLogistics(WorldGraph world, HashSet<BuildableId> machines)
    {
        HashSet<BuildableId> owned = [];
        HashSet<BuildableId> seen = [];
        foreach (Buildable start in machines.SelectMany(id => Neighbours(world, id)).OfType<Logistics>())
        {
            if (!seen.Add(start.Id))
            {
                continue;
            }

            List<BuildableId> line = [start.Id];
            bool onlyOurs = true;
            Queue<BuildableId> toVisit = new([start.Id]);
            while (toVisit.TryDequeue(out BuildableId? current))
            {
                foreach (Buildable next in Neighbours(world, current))
                {
                    if (next is Logistics && seen.Add(next.Id))
                    {
                        line.Add(next.Id);
                        toVisit.Enqueue(next.Id);
                    }
                    else if (next is Machine && !machines.Contains(next.Id))
                    {
                        onlyOurs = false;
                    }
                }
            }

            if (onlyOurs)
            {
                owned.UnionWith(line);
            }
        }

        return owned;
    }

    private static IEnumerable<Buildable> Neighbours(WorldGraph world, BuildableId id) =>
        world.Upstream(id).Concat(world.Downstream(id));

    /// <summary>A factory's members in one world, and what can be read from them.</summary>
    private sealed class Trace(WorldGraph world, HashSet<BuildableId> machines, HashSet<BuildableId> logistics)
    {
        private bool IsMember(BuildableId id) => machines.Contains(id) || logistics.Contains(id);

        private IEnumerable<BuildableId> Members => machines.Concat(logistics);

        /// <summary>Every handover from a member to what isn't one, and every owned line that ends inside the factory.</summary>
        public List<FactoryConnection> Outputs() =>
        [
            .. Members
                .SelectMany(inside => Across(world.Downstream(inside), inside, logistics.Contains(inside)))
                .Distinct()
                .Select(handover => new FactoryConnection(handover.Inside, handover.Outside, Made(handover.Inside))),
        ];

        /// <summary>Every handover to a member from what isn't one, and every owned line that starts inside the factory.</summary>
        public List<FactoryConnection> Inputs() =>
        [
            .. Members
                .SelectMany(inside => Across(world.Upstream(inside), inside, logistics.Contains(inside)))
                .Distinct()
                .Select(handover => new FactoryConnection(handover.Inside, handover.Outside, Asked(handover.Inside))),
        ];

        /// <summary>
        /// The providers and unfed containers upstream of its inputs, through logistics (BR-A14). The walk stops at
        /// machines: what lies beyond a machine is another factory's supply.
        /// </summary>
        public HashSet<BuildableId> Suppliers(IEnumerable<FactoryConnection> inputs)
        {
            HashSet<BuildableId> suppliers = [];
            HashSet<BuildableId> seen = [];
            Queue<BuildableId> toVisit = new(inputs.Select(input => input.Outside ?? input.Inside));
            while (toVisit.TryDequeue(out BuildableId? current))
            {
                if (!seen.Add(current))
                {
                    continue;
                }

                switch (world.Find(current))
                {
                    case Provider:
                    case Logistics { Kind: LogisticsKind.StorageContainer or LogisticsKind.FluidBuffer } when !world.Upstream(current).Any():
                        suppliers.Add(current);
                        break;
                    case Logistics:
                        foreach (Buildable upstream in world.Upstream(current))
                        {
                            toVisit.Enqueue(upstream.Id);
                        }

                        break;
                }
            }

            return suppliers;
        }

        /// <summary>The factory's machines whose output reaches this member through its own logistics, or the member itself.</summary>
        public HashSet<BuildableId> MachinesReaching(BuildableId member) => Reach(member, world.Upstream);

        /// <summary>The handovers across the given neighbours of a member, or one to nothing when an owned line ends there.</summary>
        private IEnumerable<(BuildableId Inside, BuildableId? Outside)> Across(
            IEnumerable<Buildable> neighbours, BuildableId inside, bool isOwnedLogistics)
        {
            List<Buildable> all = neighbours.ToList();
            if (all.Count == 0 && isOwnedLogistics)
            {
                yield return (inside, null);
            }

            foreach (Buildable outside in all.Where(neighbour => !IsMember(neighbour.Id)))
            {
                yield return (inside, outside.Id);
            }
        }

        /// <summary>What the factory's machines that reach this member make.</summary>
        private List<ItemId> Made(BuildableId member) => Ordered(MachinesReaching(member).SelectMany(id => Machine(id).Produces));

        /// <summary>What the factory's machines this member reaches ask for and don't get from inside the factory.</summary>
        private List<ItemId> Asked(BuildableId member) => Ordered(Reach(member, world.Downstream).SelectMany(id =>
        {
            HashSet<ItemId> fromInside = Reach(id, world.Upstream, startsAtMachine: false)
                .SelectMany(supplier => Machine(supplier).Produces)
                .ToHashSet();
            return Machine(id).Consumes.Where(item => !fromInside.Contains(item));
        }));

        /// <summary>
        /// The factory's machines reached from a member through its own logistics, in one direction. A machine stops the
        /// walk. When the member is a machine itself, it is what's reached, unless <paramref name="startsAtMachine"/> is
        /// false: then the walk starts at its neighbours.
        /// </summary>
        private HashSet<BuildableId> Reach(
            BuildableId member, Func<BuildableId, IEnumerable<Buildable>> step, bool startsAtMachine = true)
        {
            if (startsAtMachine && machines.Contains(member))
            {
                return [member];
            }

            HashSet<BuildableId> reached = [];
            HashSet<BuildableId> seen = [member];
            Queue<BuildableId> toVisit = new([member]);
            while (toVisit.TryDequeue(out BuildableId? current))
            {
                foreach (Buildable next in step(current).Where(next => seen.Add(next.Id)))
                {
                    if (machines.Contains(next.Id))
                    {
                        reached.Add(next.Id);
                    }
                    else if (logistics.Contains(next.Id))
                    {
                        toVisit.Enqueue(next.Id);
                    }
                }
            }

            return reached;
        }

        private Machine Machine(BuildableId id) => (Machine)world.Find(id)!;

        private static List<ItemId> Ordered(IEnumerable<ItemId> items) =>
            items.Distinct().OrderBy(item => item.Id, StringComparer.Ordinal).ToList();
    }
}
