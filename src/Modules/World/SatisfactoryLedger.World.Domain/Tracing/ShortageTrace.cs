using SatisfactoryLedger.World.Domain.Flow.Objects;

namespace SatisfactoryLedger.World.Domain.Tracing;

/// <summary>
/// Where a shortage starts (decided 2026-11-20). From the starved machines asked about, each item they're short of is
/// followed upstream along their lines:
/// <list type="bullet">
/// <item>a full belt, lift or pipe on the way is where it starts, and what's behind it isn't looked at;</item>
/// <item>a producer starved itself is followed in turn, along the items it's short of;</item>
/// <item>producers at full speed and sources giving all they can: if part of what they send goes to other machines, it's
/// taken by others; otherwise there are too few producers, or the source is at its limit;</item>
/// <item>a producer that can't get its output away, a station or container whose supply is unknown, a line from nothing,
/// a machine without a recipe: each is a start of its own.</item>
/// </list>
/// Machines short of the same item are followed together, so a line they share is walked once.
/// </summary>
public static class ShortageTrace
{
    /// <param name="machines">The machines asked about; only the starved among them are traced.</param>
    public static IReadOnlyList<TraceStart> From(WorldGraph world, FlowReadings readings, IReadOnlyCollection<BuildableId> machines)
    {
        var starts = new TraceStarts();
        HashSet<(BuildableId, ItemId)> followed = [];
        List<(BuildableId Id, MachineReading Reading)> starved =
        [
            .. machines.Select(id => (id, readings.Machine(id))).Where(m => m.Item2?.Limit == MachineLimit.Starved).Select(m => (m.id, m.Item2!)),
        ];
        foreach (ItemId item in starved.SelectMany(m => m.Reading.StarvedOf.Keys).Distinct())
        {
            // Starved machines that nothing found explains get an unclear start: a trace never leaves them out.
            List<BuildableId> group = [.. starved.Where(m => m.Reading.StarvedOf.ContainsKey(item)).Select(m => m.Id)];
            bool fresh = group.Exists(id => !followed.Contains((id, item)));
            int found = starts.Found;
            Follow(group, item, []);
            if (fresh && starts.Found == found)
            {
                starts.Add(new TraceStart(TraceCause.Unclear, item, group, [new TraceStep(group, item, group.Sum(id => Missing(id, item)))], 0));
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

            IReadOnlyList<TraceStep> here = [.. path, new TraceStep(group, item, group.Sum(id => Missing(id, item)))];
            if (here.Count > TraceStarts.MostSteps)
            {
                starts.Add(new TraceStart(TraceCause.Unclear, item, group, here, 0));
                return;
            }

            UpstreamLine line = UpstreamLine.Of(world, readings, group, item);
            foreach (BuildableId belt in line.FullBelts)
            {
                starts.Add(new TraceStart(TraceCause.BeltFull, item, [belt], here, TraceStarts.Capacity(world, belt)));
            }

            if (line.Sources.Count == 0)
            {
                if (line.FullBelts.Count == 0)
                {
                    starts.Add(line.WithoutRecipe.Count > 0
                        ? new TraceStart(TraceCause.NoRecipe, item, line.WithoutRecipe, here, 0)
                        : new TraceStart(TraceCause.NothingBringsIt, item, line.LooseEnds.Count > 0 ? line.LooseEnds : group, here, 0));
                }

                return;
            }

            List<BuildableId> fullSpeed = [];
            List<BuildableId> atLimit = [];
            List<BuildableId> spare = [];
            Dictionary<ItemId, List<BuildableId>> starvedProducers = [];
            foreach (BuildableId source in line.Sources)
            {
                Buildable buildable = world.Find(source)!;
                MachineReading? reading = readings.Machine(source);
                switch (buildable)
                {
                    case Machine when reading?.Limit == MachineLimit.Starved:
                        foreach (ItemId short_ in reading.StarvedOf.Keys)
                        {
                            (starvedProducers.TryGetValue(short_, out List<BuildableId>? producers) ? producers : starvedProducers[short_] = []).Add(source);
                        }

                        break;
                    case Machine when reading?.Limit == MachineLimit.OutputBlocked:
                        starts.Add(new TraceStart(TraceCause.ProducerOutputBlocked, item, [source], here, readings.OutOf(buildable, item)));
                        break;
                    case Machine or Consumer:
                        fullSpeed.Add(source);
                        break;
                    case Provider provider when provider.IsStation || readings.IsUnknownSupply(source):
                    case Logistics:
                        starts.Add(new TraceStart(TraceCause.UnknownSupply, item, [source], here, readings.OutOf(buildable, item)));
                        break;
                    case Provider when readings.Spare(source, item) > TraceStarts.Tolerance:
                        spare.Add(source);
                        break;
                    default:
                        atLimit.Add(source);
                        break;
                }
            }

            foreach ((ItemId short_, List<BuildableId> producers) in starvedProducers)
            {
                Follow(producers, short_, here);
            }

            List<BuildableId> sending = [.. fullSpeed, .. atLimit, .. spare];
            if (sending.Count == 0)
            {
                return;
            }

            HashSet<BuildableId> asked = [.. group];
            Dictionary<BuildableId, double> others = [];
            foreach (BuildableId source in sending)
            {
                foreach (Buildable next in world.Downstream(source))
                {
                    double leaving = next is Logistics ? readings.Into(next.Id, item) : 0;
                    foreach ((BuildableId end, double perMinute) in OutputDestinations.Of(world, next.Id, item, leaving, id => readings.Into(id, item)))
                    {
                        if (!asked.Contains(end) && end != source)
                        {
                            others[end] = others.GetValueOrDefault(end) + perMinute;
                        }
                    }
                }
            }

            double sent = sending.Sum(source => readings.OutOf(world.Find(source)!, item));
            if (TraceStarts.Matters(others.Values.Sum(), sent))
            {
                starts.Add(new TraceStart(TraceCause.TakenByOthers, item, sending, here, Math.Round(sent, 6)) { Others = TraceStarts.Rounded(others) });
                return;
            }

            if (fullSpeed.Count > 0)
            {
                starts.Add(new TraceStart(TraceCause.TooFewProducers, item, fullSpeed, here, fullSpeed.Sum(id => AtFullSpeed(world.Find(id)!, item))));
            }

            if (atLimit.Count > 0)
            {
                starts.Add(new TraceStart(TraceCause.SourceAtLimit, item, atLimit, here, atLimit.Sum(id => readings.OutOf(world.Find(id)!, item))));
            }

            if (spare.Count > 0 && fullSpeed.Count == 0 && atLimit.Count == 0)
            {
                starts.Add(new TraceStart(TraceCause.Unclear, item, spare, here, spare.Sum(id => readings.OutOf(world.Find(id)!, item))));
            }
        }

        // What a starved machine lacks of an item per minute: what it asks at its clock speed less the share it gets.
        double Missing(BuildableId id, ItemId item) =>
            world.Find(id) is Machine machine && readings.Machine(id) is { } reading
                ? Math.Round((machine.Demand.GetValueOrDefault(item)?.ToDouble() ?? 0) * (1 - reading.StarvedOf.GetValueOrDefault(item, 1)), 6)
                : 0;
    }

    /// <summary>What a producer makes of an item at full speed, per minute.</summary>
    private static double AtFullSpeed(Buildable producer, ItemId item) => producer switch
    {
        Machine machine => machine.Production.GetValueOrDefault(item)?.ToDouble() ?? 0,
        Consumer { Generation: { Byproduct: { } waste } generation } when waste == item => generation.Leaves.ToDouble(),
        _ => 0,
    };

    /// <summary>
    /// The lines into some machines, followed upstream through logistics for one item, to where they start: the
    /// producers and sources of the item, a full belt (instead of what's behind it), belts that start at nothing, and
    /// machines without a recipe at a line's start. Each buildable is walked once.
    /// </summary>
    private sealed class UpstreamLine
    {
        private readonly WorldGraph _world;
        private readonly FlowReadings _readings;
        private readonly ItemId _item;
        private readonly HashSet<BuildableId> _walked;

        private UpstreamLine(WorldGraph world, FlowReadings readings, ItemId item, IEnumerable<BuildableId> machines)
        {
            _world = world;
            _readings = readings;
            _item = item;
            _walked = [.. machines];
        }

        public List<BuildableId> Sources { get; } = [];

        public List<BuildableId> FullBelts { get; } = [];

        public List<BuildableId> LooseEnds { get; } = [];

        public List<BuildableId> WithoutRecipe { get; } = [];

        public static UpstreamLine Of(WorldGraph world, FlowReadings readings, IReadOnlyList<BuildableId> machines, ItemId item)
        {
            var line = new UpstreamLine(world, readings, item, machines);
            foreach (Buildable from in machines.SelectMany(world.Upstream))
            {
                line.Walk(from.Id);
            }

            return line;
        }

        /// <returns>Whether the item can come from here: a source of it lies upstream.</returns>
        private bool Walk(BuildableId id)
        {
            if (!_walked.Add(id))
            {
                return false;
            }

            switch (_world.Find(id))
            {
                case Logistics logistics:
                    List<Buildable> upstream = [.. _world.Upstream(id)];
                    if (upstream.Count == 0)
                    {
                        if (logistics.Kind is LogisticsKind.StorageContainer or LogisticsKind.FluidBuffer)
                        {
                            return Source(logistics.Stock.ContainsKey(_item) || logistics.Inventory?.Holds.ContainsKey(_item) == true);
                        }

                        LooseEnds.Add(id);
                        return false;
                    }

                    (int sources, int belts) = (Sources.Count, FullBelts.Count);
                    bool brings = false;
                    foreach (Buildable from in upstream)
                    {
                        brings |= Walk(from.Id);
                    }

                    // A full belt limits all behind it: it's where the shortage starts, not what's behind it.
                    if (brings && _readings.IsBottleneck(id))
                    {
                        Sources.RemoveRange(sources, Sources.Count - sources);
                        FullBelts.RemoveRange(belts, FullBelts.Count - belts);
                        FullBelts.Add(id);
                    }

                    return brings;
                case Machine machine:
                    if (machine.Produces.Count == 0)
                    {
                        WithoutRecipe.Add(id);
                    }

                    return Source(machine.Produces.Contains(_item));
                case Consumer { Generation.Byproduct: { } waste }:
                    return Source(waste == _item);
                case Provider provider:
                    return Source(provider.Offer?.Item == _item || provider.IsStation || provider.Stock.ContainsKey(_item) || _readings.IsUnknownSupply(id));
                default:
                    return false;
            }

            bool Source(bool makesIt)
            {
                if (makesIt)
                {
                    Sources.Add(id);
                }

                return makesIt;
            }
        }
    }
}
