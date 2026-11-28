using SatisfactoryLedger.World.Domain.Flow.Objects;

namespace SatisfactoryLedger.World.Domain.Tracing;

/// <summary>
/// Where an output blockage starts (decided 2026-11-20), the mirror of <see cref="ShortageTrace"/>. From the
/// output-blocked machines asked about, each item that can't all leave is followed downstream along their lines:
/// <list type="bullet">
/// <item>a full belt, lift or pipe on the way is where it starts;</item>
/// <item>a consumer blocked itself is followed in turn, along the items it can't get away;</item>
/// <item>a consumer short of another input takes no more: that's where it starts;</item>
/// <item>consumers at full speed: if part of what they take comes from other producers, they're supplied by others;
/// otherwise there are too few consumers;</item>
/// <item>a full container or station, a belt to nowhere, a machine or a generator burning a known fuel that doesn't take
/// the item (decided 2026-11-28), a machine with no recipe, a drone
/// port with the item in its fuel slot where no drone refuels (issue #44): each is a start of its own;</item>
/// <item>a machine asked about with no line that carries the item has nowhere to send it (decided 2026-11-22).</item>
/// </list>
/// Blocked machines that none of these explain get an unclear start: a trace never leaves them out.
/// </summary>
public static class BlockageTrace
{
    /// <param name="machines">The machines asked about; only the output-blocked among them are traced.</param>
    /// <param name="tiers">The game's belt, lift and pipe tiers, for what a full one needs; none, and it says nothing.</param>
    public static IReadOnlyList<TraceStart> From(
        WorldGraph world, FlowReadings readings, IReadOnlyCollection<BuildableId> machines, IReadOnlyCollection<TransportTier>? tiers = null)
    {
        tiers ??= [];
        var starts = new TraceStarts();
        HashSet<(BuildableId, ItemId)> followed = [];
        List<(BuildableId Id, MachineReading Reading)> blocked =
        [
            .. machines.Select(id => (id, readings.Machine(id))).Where(m => m.Item2?.Limit == MachineLimit.OutputBlocked).Select(m => (m.id, m.Item2!)),
        ];
        foreach (ItemId item in blocked.SelectMany(m => m.Reading.BlockedOn.Keys).Distinct())
        {
            List<BuildableId> group = [.. blocked.Where(m => m.Reading.BlockedOn.ContainsKey(item)).Select(m => m.Id)];
            bool fresh = group.Exists(id => !followed.Contains((id, item)));
            int found = starts.Found;
            Follow(group, item, []);
            if (fresh && starts.Found == found)
            {
                starts.Add(new TraceStart(TraceCause.Unclear, item, group, [new TraceStep(group, item, group.Sum(id => CannotLeave(id, item)))], 0));
            }
        }

        return starts.All;

        void Follow(List<BuildableId> group, ItemId item, IReadOnlyList<TraceStep> path)
        {
            group = [.. group.Where(id => followed.Add((id, item)))];
            if (group.Count == 0)
            {
                return;
            }

            IReadOnlyList<TraceStep> here = [.. path, new TraceStep(group, item, group.Sum(id => CannotLeave(id, item)))];
            if (here.Count > TraceStarts.MostSteps)
            {
                starts.Add(new TraceStart(TraceCause.Unclear, item, group, here, 0));
                return;
            }

            DownstreamLine line = DownstreamLine.Of(world, readings, group, item);
            foreach (BuildableId belt in line.FullBelts)
            {
                starts.Add(TraceStarts.Bottleneck(world, readings, belt, item, here, tiers));
            }

            if (line.FullStores.Count > 0)
            {
                starts.Add(new TraceStart(TraceCause.StorageFull, item, line.FullStores, here, 0));
            }

            if (line.Nowhere.Count > 0)
            {
                starts.Add(new TraceStart(TraceCause.NowhereToGo, item, line.Nowhere, here, 0));
            }

            if (line.NoDroneRefuels.Count > 0)
            {
                starts.Add(new TraceStart(TraceCause.NoDroneRefuels, item, line.NoDroneRefuels, here, 0));
            }

            List<BuildableId> fullSpeed = [];
            Dictionary<ItemId, List<BuildableId>> blockedConsumers = [];
            foreach (BuildableId consumer in line.Consumers)
            {
                MachineReading? reading = readings.Machine(consumer);
                switch (reading?.Limit)
                {
                    case MachineLimit.NoRecipe:
                        starts.Add(new TraceStart(TraceCause.NoRecipe, item, [consumer], here, 0));
                        break;
                    case MachineLimit.OutputBlocked:
                        foreach (ItemId product in reading.BlockedOn.Keys)
                        {
                            (blockedConsumers.TryGetValue(product, out List<BuildableId>? consumers) ? consumers : blockedConsumers[product] = []).Add(consumer);
                        }

                        break;
                    case MachineLimit.Starved:
                        starts.Add(new TraceStart(TraceCause.ConsumerStarved, item, [consumer], here, world.Find(consumer)!.DemandOf(item).ToDouble()) { ShortOf = reading.StarvedOf });
                        break;
                    default:
                        fullSpeed.Add(consumer);
                        break;
                }
            }

            foreach ((ItemId product, List<BuildableId> consumers) in blockedConsumers)
            {
                Follow(consumers, product, here);
            }

            if (fullSpeed.Count == 0)
            {
                return;
            }

            HashSet<BuildableId> asked = [.. group];
            Dictionary<BuildableId, double> others = [];
            foreach (BuildableId consumer in fullSpeed)
            {
                foreach (Buildable from in world.Upstream(consumer).Where(from => from is Logistics))
                {
                    double arriving = readings.Into(from.Id, item);
                    foreach ((BuildableId start, double perMinute) in InputSources.Of(world, from.Id, item, arriving, readings))
                    {
                        if (!asked.Contains(start))
                        {
                            others[start] = others.GetValueOrDefault(start) + perMinute;
                        }
                    }
                }
            }

            double taken = fullSpeed.Sum(id => world.Find(id)!.DemandOf(item).ToDouble());
            starts.Add(TraceStarts.Matters(others.Values.Sum(), taken)
                ? new TraceStart(TraceCause.SuppliedByOthers, item, fullSpeed, here, taken) { Others = TraceStarts.Rounded(others) }
                : new TraceStart(TraceCause.TooFewConsumers, item, fullSpeed, here, taken));
        }

        // What a blocked machine or generator can't get away of an item per minute: what it makes at its clock speed less the share that leaves.
        double CannotLeave(BuildableId id, ItemId item) =>
            world.Find(id) is { } blocked && readings.Machine(id) is { } reading
                ? Math.Round(blocked.ProductionOf(item).ToDouble() * (1 - reading.BlockedOn.GetValueOrDefault(item, 1)), 6)
                : 0;
    }

    /// <summary>
    /// The lines out of some machines, followed downstream through logistics for one item, to where they end: the
    /// consumers that take it, a full belt (instead of what's after it), a full container or station, and belts or
    /// machines where it can go no further. Each buildable is walked once.
    /// </summary>
    private sealed class DownstreamLine
    {
        private readonly WorldGraph _world;
        private readonly FlowReadings _readings;
        private readonly ItemId _item;
        private readonly HashSet<BuildableId> _walked;

        private DownstreamLine(WorldGraph world, FlowReadings readings, ItemId item, IEnumerable<BuildableId> machines)
        {
            _world = world;
            _readings = readings;
            _item = item;
            _walked = [.. machines];
        }

        public List<BuildableId> Consumers { get; } = [];

        public List<BuildableId> FullBelts { get; } = [];

        public List<BuildableId> FullStores { get; } = [];

        public List<BuildableId> Nowhere { get; } = [];

        /// <summary>Drone ports with the item in their fuel slot where no drone refuels: they burn none of it.</summary>
        public List<BuildableId> NoDroneRefuels { get; } = [];

        public static DownstreamLine Of(WorldGraph world, FlowReadings readings, IReadOnlyList<BuildableId> machines, ItemId item)
        {
            var line = new DownstreamLine(world, readings, item, machines);
            foreach (BuildableId machine in machines)
            {
                // A machine with more than one product sends each on its own lines: only those that carry this one.
                bool oneProduct = world.Find(machine) is not Machine { Produces.Count: > 1 };
                List<Buildable> lines = [.. world.Downstream(machine).Where(next => oneProduct || line.Carries(next))];
                if (lines.Count == 0)
                {
                    // No belt or pipe takes the item away from the machine itself: an output left unconnected.
                    line.Nowhere.Add(machine);
                }

                foreach (Buildable next in lines)
                {
                    line.Walk(next.Id);
                }
            }

            return line;
        }

        private bool Carries(Buildable buildable) =>
            _readings.Into(buildable.Id, _item) > 0 || (buildable is Logistics logistics && logistics.Carrying.ContainsKey(_item));

        /// <returns>Whether the item goes this way: something downstream takes it, or holds it up.</returns>
        private bool Walk(BuildableId id)
        {
            if (!_walked.Add(id))
            {
                return false;
            }

            switch (_world.Find(id))
            {
                case Logistics logistics:
                    List<Buildable> downstream = [.. _world.Downstream(id)];
                    if (downstream.Count == 0)
                    {
                        if (logistics.Kind is LogisticsKind.StorageContainer or LogisticsKind.FluidBuffer)
                        {
                            return Store(logistics.Inventory);
                        }

                        Nowhere.Add(id);
                        return true;
                    }

                    (int consumers, int belts, int stores, int nowhere, int idle) = (Consumers.Count, FullBelts.Count, FullStores.Count, Nowhere.Count, NoDroneRefuels.Count);
                    bool goes = false;
                    foreach (Buildable next in downstream)
                    {
                        goes |= Walk(next.Id);
                    }

                    // A full belt holds up all after it: it's where the blockage starts, not what's after it.
                    if (goes && _readings.IsBottleneck(id))
                    {
                        Consumers.RemoveRange(consumers, Consumers.Count - consumers);
                        FullBelts.RemoveRange(belts, FullBelts.Count - belts);
                        FullStores.RemoveRange(stores, FullStores.Count - stores);
                        Nowhere.RemoveRange(nowhere, Nowhere.Count - nowhere);
                        NoDroneRefuels.RemoveRange(idle, NoDroneRefuels.Count - idle);
                        FullBelts.Add(id);
                    }

                    return goes;
                case Consumer { FuelBurn: FuelBurn.NoDroneRefuels } port when port.Burns.Contains(_item):
                    NoDroneRefuels.Add(id);
                    return true;
                case Provider { FuelBurn: FuelBurn.NoDroneRefuels } port when port.Burns.Contains(_item):
                    NoDroneRefuels.Add(id);
                    return true;
                // A machine with no recipe: its reading says so.
                case Machine { Consumes.Count: 0 }:
                    Consumers.Add(id);
                    return true;
                case Consumer { BurnsKnownFuel: false, Inventory: { } inventory }:
                    return Store(inventory);
                // A machine its recipe's inputs, a generator burning a known fuel what it burns (decided 2026-11-28);
                // the other consumers every item.
                case (Machine or Consumer) and { } taker when taker.Takes(_item):
                    Consumers.Add(id);
                    return true;
                default:
                    Nowhere.Add(id);
                    return true;
            }

            bool Store(Inventory? inventory)
            {
                if (inventory?.Takes(_item) == false)
                {
                    FullStores.Add(id);
                    return true;
                }

                return false;
            }
        }
    }
}
