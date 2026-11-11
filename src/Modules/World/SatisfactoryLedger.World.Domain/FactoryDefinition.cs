using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.World.Domain;

/// <summary>
/// A factory as the player defines it: its machines, generators included (BR-W2). The game has no notion of a factory,
/// so this is the player's own data. Its logistics, outputs and inputs are derived from the world (BR-W3, BR-W4).
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
    public bool IsTraceableIn(WorldGraph world) => Machines.Any(id => CanBeChosen(world.Find(id)));

    /// <summary>
    /// BR-W2: what a factory's machines can be: a machine, or a generator (decided 2026-11-11). A generator not chosen
    /// stays a consumer at the end of a line, as sinks and stations do (BR-W8).
    /// </summary>
    private static bool CanBeChosen(Buildable? buildable) => buildable is Machine or Consumer { IsGenerator: true };

    /// <summary>Finds what belongs to the factory in a world: its machines there, its logistics, outputs and inputs.</summary>
    /// <param name="fluids">The items pipes carry; belts carry the rest (BR-W4).</param>
    public FactoryMembers TraceMembers(WorldGraph world, IReadOnlySet<ItemId> fluids)
    {
        HashSet<BuildableId> machines = Machines.Where(id => CanBeChosen(world.Find(id))).ToHashSet();
        HashSet<BuildableId> logistics = OwnedLogistics(world, machines);
        var factory = new Trace(world, fluids, machines, logistics);

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
    private sealed class Trace(WorldGraph world, IReadOnlySet<ItemId> fluids, HashSet<BuildableId> machines, HashSet<BuildableId> logistics)
    {
        private bool IsMember(BuildableId id) => machines.Contains(id) || logistics.Contains(id);

        private IEnumerable<BuildableId> Members => machines.Concat(logistics);

        /// <summary>
        /// Every handover from a member to what isn't one, and every owned line that ends in its own container, carrying
        /// what can leave there. One whose line carries none of what its machines make (a pipe on a fluid port its
        /// recipe doesn't use) is no output.
        /// </summary>
        public List<FactoryConnection> Outputs() =>
        [
            .. Members
                .SelectMany(inside => Across(world.Downstream(inside), inside))
                .Distinct()
                .Select(handover => Carried(handover, Made(handover.Inside)))
                .OfType<FactoryConnection>(),
        ];

        /// <summary>
        /// Every handover to a member from what isn't one, and every owned line that starts at a container nothing feeds, carrying
        /// what can arrive there. One whose line carries none of what its machines ask for is no input.
        /// </summary>
        public List<FactoryConnection> Inputs() =>
        [
            .. Members
                .SelectMany(inside => Across(world.Upstream(inside), inside))
                .Distinct()
                .Select(handover => Carried(handover, Asked(handover.Inside)))
                .OfType<FactoryConnection>(),
        ];

        /// <summary>
        /// BR-W4: a handover carries the items of its line's form: a pipe fluids, a belt solids. Its line is the side
        /// that's logistics; between a machine and a consumer, anything goes. Null when none of the items fits.
        /// </summary>
        private FactoryConnection? Carried((BuildableId Inside, BuildableId? Outside) handover, List<ItemId> items)
        {
            Logistics? line = world.Find(handover.Inside) as Logistics
                ?? (handover.Outside is { } outside ? world.Find(outside) as Logistics : null);
            List<ItemId> carried = line is null ? items : [.. items.Where(item => fluids.Contains(item) == line.CarriesFluids)];
            return items.Count > 0 && carried.Count == 0 ? null : new FactoryConnection(handover.Inside, handover.Outside, carried);
        }

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

        /// <summary>
        /// The handovers across the given neighbours of a member, or one to nothing when an owned line ends there in a
        /// container or fluid buffer (BR-W4). A belt, lift or pipe that just stops is no handover (decided 2026-11-11).
        /// </summary>
        private IEnumerable<(BuildableId Inside, BuildableId? Outside)> Across(IEnumerable<Buildable> neighbours, BuildableId inside)
        {
            List<Buildable> all = neighbours.ToList();
            if (all.Count == 0 && logistics.Contains(inside)
                && world.Find(inside) is Logistics { Kind: LogisticsKind.StorageContainer or LogisticsKind.FluidBuffer })
            {
                yield return (inside, null);
            }

            foreach (Buildable outside in all.Where(neighbour => !IsMember(neighbour.Id)))
            {
                yield return (inside, outside.Id);
            }
        }

        /// <summary>What the factory's machines that reach this member make.</summary>
        private List<ItemId> Made(BuildableId member) => Ordered(MachinesReaching(member).SelectMany(Produces));

        /// <summary>What the factory's machines this member reaches ask for and don't get from inside the factory.</summary>
        private List<ItemId> Asked(BuildableId member) => Ordered(Reach(member, world.Downstream).SelectMany(id =>
        {
            HashSet<ItemId> fromInside = Reach(id, world.Upstream, startsAtMachine: false)
                .SelectMany(Produces)
                .ToHashSet();
            return Consumes(id).Where(item => !fromInside.Contains(item));
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

        /// <summary>What one of its machines makes: a machine its recipe's products, a generator its waste.</summary>
        private IEnumerable<ItemId> Produces(BuildableId id) => world.Find(id) switch
        {
            Machine machine => machine.Produces,
            Consumer { Generation: { Byproduct: { } waste } generation } when generation.Leaves > Rate.Zero => [waste],
            _ => [],
        };

        /// <summary>What one of its machines takes in: a machine its recipe's inputs, a generator its fuel and water.</summary>
        private IEnumerable<ItemId> Consumes(BuildableId id) => world.Find(id) switch
        {
            Machine machine => machine.Consumes,
            Consumer { Generation: { } generation } => generation.SupplementalNeeded > Rate.Zero && generation.Supplemental is { } water
                ? [generation.Fuel, water]
                : [generation.Fuel],
            _ => [],
        };

        private static List<ItemId> Ordered(IEnumerable<ItemId> items) =>
            items.Distinct().OrderBy(item => item.Id, StringComparer.Ordinal).ToList();
    }
}
