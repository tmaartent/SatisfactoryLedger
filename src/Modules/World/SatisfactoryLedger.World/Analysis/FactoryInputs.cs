using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.Domain.Tracing;
using SatisfactoryLedger.World.Factories;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Analysis;

/// <summary>
/// What a factory takes in per item (decided 2026-11-11): what arrives over its inputs now, what it needs from outside
/// at 100%, how short it is, and where what arrives comes from.
/// </summary>
internal static class FactoryInputs
{
    /// <param name="factories">The world's factories, this one among them: who a supplying machine belongs to.</param>
    public static List<FactoryInput> Of(
        SnapshotDocument snapshot,
        WorldGraph world,
        StoredFactory factory,
        FactoryMembers members,
        FlowReadings readings,
        IEnumerable<StoredFactory> factories)
    {
        Dictionary<string, BuildableDocument> buildables = snapshot.Buildables.ToDictionary(buildable => buildable.Id, StringComparer.Ordinal);
        Dictionary<BuildableId, StoredFactory> owners = FactoryOutputs.OwnersIn(snapshot, factory, members, factories);

        Dictionary<string, double> now = new(StringComparer.Ordinal);
        Dictionary<string, List<BuildableId>> startsOf = new(StringComparer.Ordinal);
        Dictionary<string, Dictionary<(InputSourceKind Kind, StoredFactory? Factory, string? Building, string? Id), double>> sources = new(StringComparer.Ordinal);

        // Per item and source, the machines and generators it starts at.
        Dictionary<(string Item, (InputSourceKind, StoredFactory?, string?, string?) Source), SortedSet<string>> startMachines = [];
        foreach (FactoryConnection input in members.Inputs)
        {
            BuildableId start = input.Outside ?? input.Inside;
            foreach (ItemId item in input.Items)
            {
                double amount = Arriving(input, item, world, readings, members);
                now[item.Id] = now.GetValueOrDefault(item.Id) + amount;

                // From its own belt or pipe when it has one, so that one's room counts too.
                startsOf.TryAdd(item.Id, []);
                startsOf[item.Id].Add(world.Find(input.Inside) is Logistics ? input.Inside : start);

                Dictionary<(InputSourceKind, StoredFactory?, string?, string?), double> from = sources.TryGetValue(item.Id, out var found) ? found : sources[item.Id] = [];
                foreach ((BuildableId source, double perMinute) in InputSources.Of(world, start, item, amount, id => readings.OutOf(id, item)))
                {
                    var key = Source(source);
                    from[key] = from.GetValueOrDefault(key) + perMinute;
                    if (key.Item1 is InputSourceKind.Factory or InputSourceKind.NoFactory)
                    {
                        (startMachines.TryGetValue((item.Id, key), out SortedSet<string>? machines) ? machines : startMachines[(item.Id, key)] = new(StringComparer.Ordinal)).Add(source.Id);
                    }
                }
            }
        }

        // At 100%, what its machines take of it less what its own machines make of it: what it needs from outside.
        Dictionary<string, double> atFullSpeed = new(StringComparer.Ordinal);
        foreach (Buildable member in members.Machines.Select(id => world.Find(id)).OfType<Buildable>())
        {
            foreach ((ItemId item, SharedKernel.Rate rate) in member.Demand.Where(input => now.ContainsKey(input.Key.Id)))
            {
                atFullSpeed[item.Id] = atFullSpeed.GetValueOrDefault(item.Id) + rate.ToDouble();
            }

            foreach ((ItemId item, SharedKernel.Rate rate) in member.Production.Where(product => now.ContainsKey(product.Key.Id)))
            {
                atFullSpeed[item.Id] = atFullSpeed.GetValueOrDefault(item.Id) - rate.ToDouble();
            }
        }

        return
        [
            .. now.Keys.Order(StringComparer.Ordinal).Select(item =>
            {
                double need = Math.Max(0, atFullSpeed.GetValueOrDefault(item));
                Dictionary<(InputSourceKind Kind, StoredFactory? Factory, string? Building, string? Id), double> from = sources.GetValueOrDefault(item) ?? [];
                return new FactoryInput(
                    item,
                    SnapshotAnalysis.Round(now[item]),
                    SnapshotAnalysis.Round(need),
                    SnapshotAnalysis.Round(Short(need, now[item] + CouldBringMore(ItemId.Of(item)))),
                    [
                        .. from.Where(entry => SnapshotAnalysis.Round(entry.Value) > 0)
                            .OrderByDescending(entry => entry.Value)
                            .Select(entry => new InputSource(
                                entry.Key.Kind,
                                entry.Key.Factory?.Id,
                                entry.Key.Factory?.Name,
                                entry.Key.Building,
                                entry.Key.Id,
                                SnapshotAnalysis.Round(entry.Value),
                                startMachines.TryGetValue((item, entry.Key), out SortedSet<string>? machines) ? [.. machines] : null))
                    ]);
            })
        ];

        // The flow settles within a small tolerance, while the need comes exactly from the recipes: a shortfall
        // within 0.01% of the need is none.
        static double Short(double need, double could) => need - could > need * 1e-4 ? need - could : 0;

        // What its sources could bring more, and what branches off into a sink, as far as the lines between have room
        // (decided 2026-11-11).
        double CouldBringMore(ItemId item) => InputSources.CouldBringMore(
            world, startsOf[item.Id], id => Spare(id, item), readings.Room, id => readings.Carries(id, item), id => Sunk(id, item));

        // What an AWESOME Sink takes of an item now: what reaches it over its lines.
        double Sunk(BuildableId id, ItemId item) => buildables.GetValueOrDefault(id.Id)?.ClassName == FactoryOutputs.SinkClass
            ? readings.Reaching(id, item)
            : 0;

        // What a source could send more per minute: a provider its unused supply; a station what its stock keeps up
        // for half an hour; a machine of another factory, or of none, what it would make more at 100%. Its own machines
        // aren't outside supply, and a container's rate isn't known: none.
        double Spare(BuildableId id, ItemId item)
        {
            if (members.Machines.Contains(id))
            {
                return 0;
            }

            return world.Find(id) switch
            {
                Provider { IsStation: true } station => StationSpare(station, item),
                Provider => readings.Spare(id, item),
                null => 0,
                _ => readings.MakesMore(id, item),
            };
        }

        // BR-A17 turned round (decided 2026-11-11): a station could deliver more as far as its stock, with that of the
        // containers its line unloads into, lasts half an hour at the higher rate. The rate trains or trucks bring
        // isn't in the save.
        double StationSpare(Provider station, ItemId item)
        {
            double delivered = readings.Delivered(station.Id, item);
            double stock = (double)(station.Stock.GetValueOrDefault(item) + Buffers(station.Id).Sum(buffer => buffer.Stock.GetValueOrDefault(item)));
            return Math.Max(0, (stock / SteadyState.ConfirmedMinutes) - delivered);
        }

        // The containers a station's line passes through, as far as the machines it reaches.
        IEnumerable<Logistics> Buffers(BuildableId station)
        {
            HashSet<BuildableId> seen = [station];
            Queue<BuildableId> toVisit = new([station]);
            while (toVisit.TryDequeue(out BuildableId? at))
            {
                foreach (Logistics next in world.Downstream(at).OfType<Logistics>().Where(next => seen.Add(next.Id)))
                {
                    if (next.Kind is LogisticsKind.StorageContainer or LogisticsKind.FluidBuffer)
                    {
                        yield return next;
                    }

                    toVisit.Enqueue(next.Id);
                }
            }
        }

        // Machines by factory; a building on its own, so two miners on one belt are two sources (decided 2026-11-11).
        (InputSourceKind, StoredFactory?, string?, string?) Source(BuildableId start) => world.Find(start) switch
        {
            Machine or Consumer { IsGenerator: true } when owners.TryGetValue(start, out StoredFactory? owner) => (InputSourceKind.Factory, owner, null, null),
            Machine or Consumer { IsGenerator: true } => (InputSourceKind.NoFactory, null, null, null),
            Logistics { Kind: LogisticsKind.Belt or LogisticsKind.Lift or LogisticsKind.Pipe or LogisticsKind.PipeNetwork } => (InputSourceKind.Nowhere, null, null, null),
            _ => (InputSourceKind.Building, null, buildables.GetValueOrDefault(start.Id)?.ClassName, start.Id),
        };
    }

    /// <summary>
    /// What arrives over an input of one item now, read from the stored flows as for an output:
    /// <list type="bullet">
    /// <item>from a container nothing feeds: what flows into the logistics it feeds;</item>
    /// <item>into logistics with one way in: what flows into it;</item>
    /// <item>from logistics with one way out: what flows into that;</item>
    /// <item>otherwise (a machine fed from a splitter or pipe network shared with others): what the machine takes at the
    /// rate it runs, shared over its inputs that bring the item.</item>
    /// </list>
    /// </summary>
    internal static double Arriving(FactoryConnection input, ItemId item, WorldGraph world, FlowReadings readings, FactoryMembers members)
    {
        if (input.Outside is not { } outside)
        {
            return world.Downstream(input.Inside).Sum(next => readings.Carries(next.Id, item));
        }

        if (world.Find(input.Inside) is Logistics && world.Upstream(input.Inside).Count() == 1)
        {
            return readings.Carries(input.Inside, item);
        }

        if (world.Find(outside) is Logistics && world.Downstream(outside).Count() == 1)
        {
            return readings.Carries(outside, item);
        }

        return readings.Takes(input.Inside, item) / members.Inputs.Count(other => other.Inside == input.Inside && other.Items.Contains(item));
    }
}
