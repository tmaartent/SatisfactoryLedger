using SatisfactoryLedger.World.Contracts.Analysis;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;
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
        AnalysisDocument analysis,
        IEnumerable<StoredFactory> factories)
    {
        Dictionary<string, MachineStateDocument> states = analysis.Machines.ToDictionary(state => state.Id, StringComparer.Ordinal);
        Dictionary<string, BuildableDocument> buildables = snapshot.Buildables.ToDictionary(buildable => buildable.Id, StringComparer.Ordinal);
        Dictionary<string, UnusedSupplyDocument> unused = analysis.UnusedSupply.ToDictionary(supply => $"{supply.Id}|{supply.Item}", StringComparer.Ordinal);
        Dictionary<string, UnknownSupplyDocument> unknown = analysis.UnknownSupply.ToDictionary(supply => supply.Id, StringComparer.Ordinal);
        Dictionary<BuildableId, StoredFactory> owners = FactoryOutputs.OwnersIn(snapshot, factory, members, factories);

        Dictionary<string, double> now = new(StringComparer.Ordinal);
        Dictionary<string, List<BuildableId>> startsOf = new(StringComparer.Ordinal);
        Dictionary<string, Dictionary<(InputSourceKind Kind, StoredFactory? Factory, string? Building, string? Id), double>> sources = new(StringComparer.Ordinal);
        foreach (FactoryConnection input in members.Inputs)
        {
            BuildableId start = input.Outside ?? input.Inside;
            foreach (ItemId item in input.Items)
            {
                double amount = Arriving(input, item, world, analysis, members, states);
                now[item.Id] = now.GetValueOrDefault(item.Id) + amount;

                // From its own belt or pipe when it has one, so that one's room counts too.
                startsOf.TryAdd(item.Id, []);
                startsOf[item.Id].Add(world.Find(input.Inside) is Logistics ? input.Inside : start);

                Dictionary<(InputSourceKind, StoredFactory?, string?, string?), double> from = sources.TryGetValue(item.Id, out var found) ? found : sources[item.Id] = [];
                foreach ((BuildableId source, double perMinute) in InputSources.Of(world, start, item, amount, id => FlowOutOf(id, item)))
                {
                    var key = Source(source);
                    from[key] = from.GetValueOrDefault(key) + perMinute;
                }
            }
        }

        // At 100%, what its machines take of it less what its own machines make of it: what it needs from outside.
        Dictionary<string, double> atFullSpeed = new(StringComparer.Ordinal);
        foreach (Buildable member in members.Machines.Select(id => world.Find(id)).OfType<Buildable>())
        {
            foreach ((ItemId item, SharedKernel.Rate rate) in FactoryOutputs.Takes(member).Where(input => now.ContainsKey(input.Key.Id)))
            {
                atFullSpeed[item.Id] = atFullSpeed.GetValueOrDefault(item.Id) + rate.ToDouble();
            }

            foreach ((ItemId item, SharedKernel.Rate rate) in FactoryOutputs.Makes(member).Where(product => now.ContainsKey(product.Key.Id)))
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
                            .Select(entry => new InputSource(entry.Key.Kind, entry.Key.Factory?.Id, entry.Key.Factory?.Name, entry.Key.Building, entry.Key.Id, SnapshotAnalysis.Round(entry.Value)))
                    ]);
            })
        ];

        // The flow settles within a small tolerance, while the need comes exactly from the recipes: a shortfall
        // within 0.01% of the need is none.
        static double Short(double need, double could) => need - could > need * 1e-4 ? need - could : 0;

        // What its sources could bring more, and what branches off into a sink, as far as the lines between have room
        // (decided 2026-11-11).
        double CouldBringMore(ItemId item) => InputSources.CouldBringMore(
            world, startsOf[item.Id], id => Spare(id, item), Room, id => Carries(id, item, []), id => Sunk(id, item));

        // What an AWESOME Sink takes of an item now: what the logistics that end at it carry.
        double Sunk(BuildableId id, ItemId item) => buildables.GetValueOrDefault(id.Id)?.ClassName == FactoryOutputs.SinkClass
            ? world.Upstream(id).Sum(from => Carries(from.Id, item, []))
            : 0;

        // What a piece of logistics carries of an item now: the stored flow into a belt, lift, pipe or container, none
        // when it has no entry (nothing flows). A splitter, merger or pipe network isn't in the stored flows at all, so
        // what the logistics that feed it carry.
        double Carries(BuildableId id, ItemId item, HashSet<BuildableId> passed) => world.Find(id) switch
        {
            Logistics { Kind: LogisticsKind.Splitter or LogisticsKind.Merger or LogisticsKind.PipeJunction or LogisticsKind.PipeNetwork } when passed.Add(id) =>
                world.Upstream(id).Where(from => from is Logistics).Sum(from => Carries(from.Id, item, passed)),
            Logistics => Into(id, item),
            _ => 0,
        };

        // What a source could send more per minute: a provider its unused supply; a station what its stock keeps up
        // for half an hour; a machine of another factory, or of none, what it would make more at 100%. Its own machines
        // aren't outside supply, and a container's rate isn't known: none.
        double Spare(BuildableId id, ItemId item)
        {
            if (members.Machines.Contains(id))
            {
                return 0;
            }

            double idle = 1 - (states.GetValueOrDefault(id.Id)?.Utilisation ?? 1);
            return world.Find(id) switch
            {
                Machine machine => idle * (machine.Production.GetValueOrDefault(item)?.ToDouble() ?? 0),
                Consumer { Generation: { Byproduct: { } waste } generation } when waste == item => idle * generation.Leaves.ToDouble(),
                Provider { IsStation: true } station => StationSpare(station, item),
                Provider when unused.TryGetValue($"{id.Id}|{item.Id}", out UnusedSupplyDocument? supply) => supply.Offered - supply.Delivered,
                _ => 0,
            };
        }

        // BR-A17 turned round (decided 2026-11-11): a station could deliver more as far as its stock, with that of the
        // containers its line unloads into, lasts half an hour at the higher rate. The rate trains or trucks bring
        // isn't in the save.
        double StationSpare(Provider station, ItemId item)
        {
            double delivered = unknown.GetValueOrDefault(station.Id.Id)?.Delivered.GetValueOrDefault(item.Id) ?? 0;
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

        // What a belt, lift or pipe could carry more: its capacity less what flows into it now, of all items. No
        // capacity known (a splitter, merger, pipe network or container): no limit.
        double Room(BuildableId id) => world.Find(id) is Logistics { Capacity: { } capacity }
            ? capacity.ToDouble() - (analysis.Into.GetValueOrDefault(id.Id)?.Values.Sum() ?? 0)
            : double.PositiveInfinity;

        double Into(BuildableId id, ItemId item) => analysis.Into.GetValueOrDefault(id.Id)?.GetValueOrDefault(item.Id) ?? 0;

        // What a buildable sends on, of one item: the stored flow into a belt, pipe or container; for a machine or
        // generator what it makes at the rate it runs; for a provider what it delivers.
        double FlowOutOf(BuildableId id, ItemId item)
        {
            if (analysis.Into.TryGetValue(id.Id, out IReadOnlyDictionary<string, double>? flows))
            {
                return flows.GetValueOrDefault(item.Id);
            }

            double utilisation = states.GetValueOrDefault(id.Id)?.Utilisation ?? 0;
            return world.Find(id) switch
            {
                Machine machine => utilisation * (machine.Production.GetValueOrDefault(item)?.ToDouble() ?? 0),
                Consumer { Generation: { Byproduct: { } waste } generation } when waste == item => utilisation * generation.Leaves.ToDouble(),
                Provider when unused.TryGetValue($"{id.Id}|{item.Id}", out UnusedSupplyDocument? supply) => supply.Delivered,
                Provider when unknown.TryGetValue(id.Id, out UnknownSupplyDocument? supply) => supply.Delivered.GetValueOrDefault(item.Id),
                Provider { Offer: { } offer } when offer.Item == item => offer.PerMinute.ToDouble(),
                _ => 0,
            };
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
    internal static double Arriving(
        FactoryConnection input,
        ItemId item,
        WorldGraph world,
        AnalysisDocument analysis,
        FactoryMembers members,
        IReadOnlyDictionary<string, MachineStateDocument> states)
    {
        double Into(BuildableId id) => analysis.Into.GetValueOrDefault(id.Id)?.GetValueOrDefault(item.Id) ?? 0;

        if (input.Outside is not { } outside)
        {
            return world.Downstream(input.Inside).Sum(next => Into(next.Id));
        }

        if (world.Find(input.Inside) is Logistics && world.Upstream(input.Inside).Count() == 1)
        {
            return Into(input.Inside);
        }

        if (world.Find(outside) is Logistics && world.Downstream(outside).Count() == 1)
        {
            return Into(outside);
        }

        double takes = (states.GetValueOrDefault(input.Inside.Id)?.Utilisation ?? 0) * world.Find(input.Inside) switch
        {
            Machine machine => machine.Demand.GetValueOrDefault(item)?.ToDouble() ?? 0,
            Consumer { Generation: { } generation } when generation.Fuel == item => generation.Burns.ToDouble(),
            Consumer { Generation: { Supplemental: { } water } generation } when water == item => generation.SupplementalNeeded.ToDouble(),
            _ => 0,
        };
        return takes / members.Inputs.Count(other => other.Inside == input.Inside && other.Items.Contains(item));
    }
}
