using SatisfactoryLedger.World.Analysis;
using SatisfactoryLedger.World.Contracts.Analysis;
using SatisfactoryLedger.World.Contracts.Arrangement;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Arrangement;
using SatisfactoryLedger.World.Domain.Tracing;
using SatisfactoryLedger.World.Factories;
using SatisfactoryLedger.World.SaveFile;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Arrangement;

/// <summary>
/// The optimal arrangement's problem made from a world and its stored analysis (decided 2026-11-12):
/// <list type="bullet">
/// <item>The area: the rectangle drawn on the map (everything built when none is), divided into plots in rows and
/// columns with roads between them; a cell of the problem is a plot and its road.</item>
/// <item>The factories inside it (their machines' middle): each on one plot, where its machines' middle is (decided
/// 2026-11-18: every factory is built to fit a plot); or, on footprints (decided 2026-11-25), on the whole foundations
/// its machines cover, with the roads kept between factories as gaps.</item>
/// <item>Their links: what inputs bring now (the sources of its <see cref="FactoryHandovers"/>, BR-A19) from another factory, to or from a
/// factory inside, each item weighing the belts (or, for a fluid, pipes) it needs: its rate over the top tier's
/// capacity, rounded up.
/// Inputs from nodes and stations don't count, as a first step (decided 2026-11-12).</item>
/// <item>Links with nodes' extractors, stations and drone ports (decided 2026-11-18): what they bring the arranged
/// factories, and what the factories send to stations and drone ports, at the weight of the settings; each building
/// at its spot on the belt floor, where its belts are routed to. A node doesn't block: factories may stand over it.
/// Extractors, stations and drone ports feeding a factory one item share belts up to the top belt's (or pipe's)
/// capacity, and so do stations and drone ports a factory sends one item to (<see cref="SupplyBelts"/>).</item>
/// <item>Machines no factory owns, and factories outside the area, that feed an arranged factory (decided 2026-11-25):
/// like a node's extractor, at the weight of the settings, from a spot at the one of their machines nearest the factory
/// as it stands now; one outside the area reached where its existing belt comes into it.</item>
/// <item>What stays: the other factories and every train or truck station and drone port, which keep the plots they
/// touch; every machine no factory owns that reaches into the area, which keeps the plots it touches too (decided
/// 2026-11-25); and the exclusions drawn, whose plots are kept free.</item>
/// </list>
/// </summary>
internal static class WorldArrangement
{
    public static MadeArrangement Of(
        SnapshotDocument snapshot, AnalysisDocument analysis, IReadOnlyList<StoredFactory> factories, ArrangementSettings settings) =>
        Make(snapshot, analysis, factories, settings);

    /// <summary>
    /// Where things stand only: the factories to arrange and what blocks the plots, without links. Enough to tell whether
    /// the factories fit, in a second instead of the fifteen the links take on a large world (2026-11-18).
    /// </summary>
    public static ArrangementProblem Placing(SnapshotDocument snapshot, IReadOnlyList<StoredFactory> factories, ArrangementSettings settings) =>
        Make(snapshot, analysis: null, factories, settings).Problem;

    /// <param name="analysis">The flows that make the links; none without it.</param>
    private static MadeArrangement Make(
        SnapshotDocument snapshot, AnalysisDocument? analysis, IReadOnlyList<StoredFactory> factories, ArrangementSettings settings)
    {
        Dictionary<string, BuildableDocument> buildables = snapshot.Buildables.ToDictionary(buildable => buildable.Id, StringComparer.Ordinal);
        Dictionary<string, FootprintDocument> footprints = (snapshot.Footprints ?? []).ToDictionary(footprint => footprint.Id, StringComparer.Ordinal);
        WorldRectangle area = settings.Area ?? Around([.. snapshot.Buildables.SelectMany(buildable => Outline(buildable, footprints))]);
        ArrangementGrid grid = ArrangementGrid.Of(area.X0, area.Y0, settings);
        (int plotsX, int plotsY) = grid.PlotsIn(area.X1 - area.X0, area.Y1 - area.Y0);

        // The belt floor: the whole plots from the area's corner, with their roads. The area can reach past it.
        var floorArea = new WorldRectangle(area.X0, area.Y0, area.X0 + (plotsX * grid.PitchX), area.Y0 + (plotsY * grid.PitchY));

        var inSnapshot = SnapshotFactories.Of(snapshot, factories);
        WorldGraph world = inSnapshot.World;
        FlowReadings? readings = analysis is null ? null : AnalysisReadings.Of(world, analysis);
        HashSet<ItemId> fluids = [.. snapshot.Fluids.Select(ItemId.Of)];
        (double belt, double pipe) = TopCapacities(snapshot);

        // Every factory traced, with the outline of its machines; those whose middle is inside the area are arranged.
        List<(TracedFactory Traced, List<(double X, double Y)> Outline)> traced = [];
        foreach (TracedFactory factory in inSnapshot.Factories.Where(factory => factory.IsTraceable))
        {
            List<(double X, double Y)> outline = [.. factory.Members.Machines
                .Select(id => buildables.GetValueOrDefault(id.Id))
                .OfType<BuildableDocument>()
                .SelectMany(buildable => Outline(buildable, footprints))];
            if (outline.Count > 0)
            {
                traced.Add((factory, outline));
            }
        }

        HashSet<string> chosen = [.. traced.Where(entry => Inside(entry.Outline, area)).Select(entry => entry.Traced.Factory.Id.ToString())];

        // Where the existing belts from buildings wholly outside the area come into it (decided 2026-11-18): followed from
        // the building to the machines of the factories it supplies (or that send to it), not any arranged factory's: a
        // freight platform unloads onto belts of every item (2026-11-18: rubber was followed round the outside to
        // another factory). Of buildings merged onto one belt, the one with the shortest way outside.
        Dictionary<string, HashSet<string>> machinesOf = traced
            .Where(entry => chosen.Contains(entry.Traced.Factory.Id.ToString()))
            .ToDictionary(entry => entry.Traced.Factory.Id.ToString(), entry => entry.Traced.Members.Machines.Select(machine => machine.Id).ToHashSet(StringComparer.Ordinal));
        HashSet<string> arrangedMachines = [.. machinesOf.Values.SelectMany(machines => machines)];
        Dictionary<string, HashSet<string>> feeds = new(StringComparer.Ordinal);
        void Feeds(string building, string factory)
        {
            (feeds.TryGetValue(building, out HashSet<string>? found) ? found : feeds[building] = new(StringComparer.Ordinal)).Add(factory);
        }

        Dictionary<string, SpotEntry> entries = new(StringComparer.Ordinal);
        ILookup<string, string> pipes = snapshot.PipeNeighbours();
        void Enter(string id, IEnumerable<string> standing, List<(double X, double Y)> outline)
        {
            // Only for a building off the belt floor: one on it is reached where it stands. One wholly outside the area
            // comes in where its belt crosses the area's edge; one on the area's edge but off the floor, where its belt
            // reaches the floor's (2026-11-19: such a miner's belt left the floor 280 metres from it, nothing showing
            // the rest).
            if (entries.ContainsKey(id) || Touches(outline, floorArea))
            {
                return;
            }

            WorldRectangle crossing = Touches(outline, area) ? floorArea : area;

            ExistingWay? shortest = null;
            foreach (string building in standing)
            {
                HashSet<string> targets = [.. (feeds.GetValueOrDefault(building) ?? []).SelectMany(factory => machinesOf.GetValueOrDefault(factory) ?? [])];
                if (ExistingWays.Into(world, buildables, pipes, building, targets.Count > 0 ? targets : arrangedMachines, crossing) is { } way
                    && (shortest is null || Length(way.Outside) < Length(shortest.Outside)))
                {
                    shortest = way;
                }
            }

            if (shortest is not null)
            {
                GridRectangle at = grid.SquaresTouching([(shortest.Entry.X, shortest.Entry.Y)], settings.Floor);

                // A few squares round where it comes in, so the belts of a station can leave side by side.
                entries[id] = new SpotEntry(new GridRectangle(at.X - 2, at.Y - 2, at.Width + 4, at.Depth + 4), shortest.Outside);
            }
        }
        List<ArrangedFactory> arranged = [];
        Dictionary<string, FixedPlace> fixedPlaces = new(StringComparer.Ordinal);
        // Per link between factories and item, what it carries: rounded to belts only once it's all added up.
        Dictionary<(string From, string To, string Item), double> factoryRates = [];
        BeltFloor floor = settings.Floor;
        Dictionary<string, GridRectangle> spots = new(StringComparer.Ordinal);
        // Per link and item, what it carries: rounded to belts only once it's all added up, since a factory's outputs
        // can reach one station over several lines (2026-11-18: four Manufacturers' 2.5 Singularity Cells a minute to a
        // drone port were four belts).
        Dictionary<(string From, string To, string Item), double> spotRates = [];
        List<(double X, double Y)> OutlineOf(IEnumerable<string> standing) => [.. standing
            .Select(buildables.GetValueOrDefault)
            .OfType<BuildableDocument>()
            .SelectMany(building => Outline(building, footprints))];
        void AddSpot(string id, IEnumerable<string> standing, string from, string to, string item, double perMinute)
        {
            List<(double X, double Y)> outline = OutlineOf(standing);
            if (outline.Count == 0 || perMinute <= 0)
            {
                return;
            }

            spots.TryAdd(id, grid.SquaresTouching(outline, floor));
            Enter(id, standing, outline);

            // A station or drone port blocks its plots below; a node doesn't.
            fixedPlaces.TryAdd(id, new FixedPlace(id, grid.PlotsTouching(outline), Blocks: false));
            spotRates[(from, to, item)] = spotRates.GetValueOrDefault((from, to, item)) + perMinute;
        }

        // What the arranged factories send to stations and drone ports, per station and item: for the supply lines.
        List<(string From, Supply Station, string Item)> stationRates = [];

        // Per arranged factory and item, the extractors, stations and drone ports that feed it: merged onto belts afterwards.
        Dictionary<(string To, string Item), List<Supply>> supplies = [];
        foreach ((TracedFactory factory, List<(double X, double Y)> outline) in traced)
        {
            Guid factoryId = factory.Factory.Id;
            string to = factoryId.ToString();
            if (chosen.Contains(to))
            {
                arranged.Add(new ArrangedFactory(to, settings.Footprints ? grid.PlotsTouching(outline) : grid.PlotOf(outline)));
            }
            else
            {
                fixedPlaces[to] = new FixedPlace(to, grid.PlotsTouching(outline), Blocks: true);
            }

            // The sources of what the factory takes in, with the machines they start at. Rounded as the factory page
            // shows them, and left out when they round to nothing, as when the arrangement read the page's inputs (#54).
            foreach (IncomingItem input in readings is null ? [] : SnapshotHandovers.Of(snapshot, inSnapshot, factory, readings).Inputs)
            {
                string item = input.Item.Id;
                foreach ((IncomingSource source, double perMinute) in SnapshotAnalysis.Shown(input.Sources, source => source.PerMinute))
                {
                    // A node's extractor, a station or a drone port feeding a factory being arranged: merged with the
                    // others that bring it the item.
                    if (chosen.Contains(to) && source is { Kind: InputSourceKind.Building, Id.Id: { } building } && IsSpot(building)
                        && buildables.GetValueOrDefault(building)?.Position is { } position)
                    {
                        List<Supply> feeding = supplies.TryGetValue((to, item), out List<Supply>? found) ? found : supplies[(to, item)] = [];
                        feeding.Add(new Supply(building, position.X, position.Y, perMinute));
                        Feeds(building, to);
                    }

                    // Machines no factory owns, or a factory outside the area, feeding a factory being arranged: like a
                    // node's extractor, from a spot at the machine nearest the factory (decided 2026-11-25).
                    bool fromOutside = source is { Kind: InputSourceKind.Factory, Owner: { } supplier } && supplier != factoryId
                        && !chosen.Contains(supplier.ToString());
                    if (chosen.Contains(to) && (source.Kind == InputSourceKind.NoFactory || fromOutside))
                    {
                        if (Nearest(source.Machines.Select(machine => machine.Id), outline) is { } machine)
                        {
                            List<Supply> feeding = supplies.TryGetValue((to, item), out List<Supply>? found) ? found : supplies[(to, item)] = [];
                            feeding.Add(new Supply(machine.Id, machine.Position!.X, machine.Position.Y, perMinute));
                            Feeds(machine.Id, to);
                        }

                        continue;
                    }

                    string? from = source is { Kind: InputSourceKind.Factory, Owner: { } other } && other != factoryId ? other.ToString() : null;

                    // Only links that touch a factory being arranged change with the arrangement.
                    if (from is null || !(chosen.Contains(to) || chosen.Contains(from)))
                    {
                        continue;
                    }

                    factoryRates[(from, to, item)] = factoryRates.GetValueOrDefault((from, to, item)) + perMinute;
                }
            }
        }

        // Extractors, stations and drone ports feeding a factory one item share belts up to the top belt's capacity
        // (decided 2026-11-18): each shared belt starts at a lift over the buildings it merges.
        foreach (((string to, string item), List<Supply> feeding) in supplies)
        {
            foreach (IReadOnlyList<Supply> merged in SupplyBelts.Merge(feeding, fluids.Contains(ItemId.Of(item)) ? pipe : belt))
            {
                string id = merged.Count == 1 ? merged[0].Id : $"{merged[0].Id}+{merged.Count - 1}>{to}";
                AddSpot(id, merged.Select(supply => supply.Id), id, to, item, merged.Sum(supply => supply.PerMinute));
            }
        }

        // What the arranged factories send to stations and drone ports, merged the same way (decided 2026-11-18): a
        // shared belt leaves the factory and goes down a lift over the stations it serves. A factory's outputs can reach
        // one station over several lines, so each station's rate is added up first.
        foreach ((TracedFactory factory, _) in traced.Where(entry => readings is not null && chosen.Contains(entry.Traced.Factory.Id.ToString())))
        {
            string from = factory.Factory.Id.ToString();
            IEnumerable<IGrouping<ItemId, Supply>> sent = FactoryHandovers.Ends(world, readings!, factory.Members)
                .Where(output => IsStation(output.End.Id) && buildables[output.End.Id].Position is not null)
                .GroupBy(output => (output.End.Id, output.Item))
                .GroupBy(
                    station => station.Key.Item,
                    station => new Supply(
                        station.Key.Id,
                        buildables[station.Key.Id].Position!.X,
                        buildables[station.Key.Id].Position!.Y,
                        station.Sum(output => output.PerMinute)));
            foreach (IGrouping<ItemId, Supply> served in sent)
            {
                stationRates.AddRange(served.Select(station => (from, station, served.Key.Id)));
                foreach (Supply station in served)
                {
                    Feeds(station.Id, from);
                }

                foreach (IReadOnlyList<Supply> merged in SupplyBelts.Merge(served, fluids.Contains(served.Key) ? pipe : belt))
                {
                    string id = merged.Count == 1 ? merged[0].Id : $"{from}>{merged[0].Id}+{merged.Count - 1}";
                    AddSpot(id, merged.Select(station => station.Id), from, id, served.Key.Id, merged.Sum(station => station.PerMinute));
                }
            }
        }

        // Every station keeps its plots, whether or not it feeds a factory.
        foreach (BuildableDocument station in snapshot.Buildables.Where(buildable => buildable.BuildableKind == BuildableKind.Station && buildable.Position is not null))
        {
            fixedPlaces[station.Id] = new FixedPlace(station.Id, grid.PlotsTouching([.. Outline(station, footprints)]), Blocks: true);
        }

        // Every machine no factory owns that reaches into the area keeps its plots, as a station does (decided 2026-11-25).
        HashSet<string> owned = [.. traced.SelectMany(entry => entry.Traced.Members.Machines.Select(machine => machine.Id))];
        foreach (BuildableDocument machine in snapshot.Buildables.Where(buildable => buildable.Position is not null && !owned.Contains(buildable.Id)
            && world.Find(BuildableId.Of(buildable.Id)) is Machine or Consumer { IsGenerator: true }))
        {
            List<(double X, double Y)> outline = [.. Outline(machine, footprints)];
            if (Touches(outline, area))
            {
                fixedPlaces[machine.Id] = new FixedPlace(machine.Id, grid.PlotsTouching(outline), Blocks: true);
            }
        }

        // Where the player keeps factories out, as places that stay and block.
        foreach ((WorldRectangle exclusion, int index) in settings.Exclusions.Select((exclusion, index) => (exclusion, index)))
        {
            string id = $"exclusion.{index}";
            fixedPlaces[id] = new FixedPlace(id, grid.PlotsTouching([(exclusion.X0, exclusion.Y0), (exclusion.X1, exclusion.Y1)]), Blocks: true);
        }

        // Each link's belts and pipes, item by item, each filled up to the top tier's capacity: what they carry, and how
        // many there are.
        Dictionary<(string From, string To), IReadOnlyList<BeltLoadDocument>> loads = [];
        List<ArrangementLink> LinksOf(Dictionary<(string From, string To, string Item), double> rates, int weightPercent)
        {
            List<ArrangementLink> made = [];
            foreach (IGrouping<(string From, string To), KeyValuePair<(string From, string To, string Item), double>> link in rates.GroupBy(rate => (rate.Key.From, rate.Key.To)))
            {
                List<BeltLoadDocument> carried = [.. link
                    .OrderBy(rate => rate.Key.Item, StringComparer.Ordinal)
                    .SelectMany(rate => fluids.Contains(ItemId.Of(rate.Key.Item))
                        ? SupplyBelts.Loads(rate.Value, pipe).Select(load => new BeltLoadDocument(rate.Key.Item, load, Pipe: true))
                        : SupplyBelts.Loads(rate.Value, belt).Select(load => new BeltLoadDocument(rate.Key.Item, load, Pipe: false)))];
                if (carried.Count > 0)
                {
                    loads[link.Key] = carried;
                    made.Add(new ArrangementLink(link.Key.From, link.Key.To, carried.Count(load => !load.Pipe), weightPercent, carried.Count(load => load.Pipe)));
                }
            }

            return made;
        }

        IReadOnlyList<ItemSupplyDocument>? lines = settings.SupplyLines ? SuppliesOf() : null;

        // Supply lines (decided 2026-11-18, BR-S): per item, everything that brings it and everything that takes it, on
        // the floor. The extractors, stations and drone ports bringing an item merge onto belts as before, but whichever
        // factories they feed; a factory brings what it sends to the others, and a station takes what it's sent.
        List<ItemSupplyDocument> SuppliesOf()
        {
            Dictionary<string, Dictionary<string, double>> sources = [];
            Dictionary<string, Dictionary<string, double>> consumers = [];
            void Add(Dictionary<string, Dictionary<string, double>> into, string item, string id, double perMinute)
            {
                Dictionary<string, double> ends = into.TryGetValue(item, out Dictionary<string, double>? found) ? found : into[item] = new(StringComparer.Ordinal);
                ends[id] = ends.GetValueOrDefault(id) + perMinute;
            }

            foreach (IGrouping<string, KeyValuePair<(string To, string Item), List<Supply>>> item in supplies.GroupBy(supply => supply.Key.Item))
            {
                IEnumerable<Supply> buildings = item
                    .SelectMany(supply => supply.Value)
                    .GroupBy(supply => supply.Id)
                    .Select(building => building.First() with { PerMinute = building.Sum(supply => supply.PerMinute) });
                foreach (IReadOnlyList<Supply> merged in SupplyBelts.Merge(buildings, fluids.Contains(ItemId.Of(item.Key)) ? pipe : belt))
                {
                    string id = merged.Count == 1 ? merged[0].Id : $"{merged[0].Id}+{merged.Count - 1}";
                    List<(double X, double Y)> outline = OutlineOf(merged.Select(supply => supply.Id));
                    if (outline.Count > 0)
                    {
                        spots.TryAdd(id, grid.SquaresTouching(outline, floor));
                        Enter(id, merged.Select(supply => supply.Id), outline);
                        Add(sources, item.Key, id, merged.Sum(supply => supply.PerMinute));
                    }
                }

                foreach (KeyValuePair<(string To, string Item), List<Supply>> feeding in item)
                {
                    Add(consumers, item.Key, feeding.Key.To, feeding.Value.Sum(supply => supply.PerMinute));
                }
            }

            foreach (KeyValuePair<(string From, string To, string Item), double> rate in factoryRates.Where(rate => chosen.Contains(rate.Key.From) && chosen.Contains(rate.Key.To)))
            {
                Add(sources, rate.Key.Item, rate.Key.From, rate.Value);
                Add(consumers, rate.Key.Item, rate.Key.To, rate.Value);
            }

            foreach ((string from, Supply station, string item) in stationRates)
            {
                List<(double X, double Y)> outline = OutlineOf([station.Id]);
                if (outline.Count > 0)
                {
                    spots.TryAdd(station.Id, grid.SquaresTouching(outline, floor));
                    Enter(station.Id, [station.Id], outline);
                    Add(sources, item, from, station.PerMinute);
                    Add(consumers, item, station.Id, station.PerMinute);
                }
            }

            static List<SupplyEndDocument> Ends(Dictionary<string, Dictionary<string, double>> of, string item) =>
                [.. of.GetValueOrDefault(item, []).OrderBy(end => end.Key, StringComparer.Ordinal).Select(end => new SupplyEndDocument(end.Key, end.Value))];
            return [.. sources.Keys.Union(consumers.Keys).Order(StringComparer.Ordinal).Select(item =>
            {
                bool fluid = fluids.Contains(ItemId.Of(item));
                return new ItemSupplyDocument(item, fluid, fluid ? pipe : belt, Ends(sources, item), Ends(consumers, item));
            })];
        }

        // On plots the roads come with them: no gap besides them. On footprints the roads are the gaps.
        (int gapX, int gapY) = settings.Gaps;
        var problem = new ArrangementProblem(
            arranged,
            [.. fixedPlaces.Values],
            [.. LinksOf(factoryRates, 100), .. LinksOf(spotRates, (int)Math.Round(settings.SpotWeight * 100))],
            new GridRectangle(0, 0, plotsX, plotsY),
            gapX,
            gapY,
            settings.Together);
        return new MadeArrangement(problem, grid, spots, loads, lines, entries);

        // Of some machines, the one standing nearest the middle of a factory's outline; none without a position.
        BuildableDocument? Nearest(IEnumerable<string> machines, List<(double X, double Y)> outline)
        {
            double x = (outline.Min(point => point.X) + outline.Max(point => point.X)) / 2;
            double y = (outline.Min(point => point.Y) + outline.Max(point => point.Y)) / 2;
            return machines
                .Select(buildables.GetValueOrDefault)
                .OfType<BuildableDocument>()
                .Where(machine => machine.Position is not null)
                .MinBy(machine => Math.Pow(machine.Position!.X - x, 2) + Math.Pow(machine.Position.Y - y, 2));
        }

        // A node's extractor, or a station or drone port: what a belt may go down to at its spot.
        bool IsSpot(string id) => world.Find(BuildableId.Of(id)) is Provider || IsStation(id);
        bool IsStation(string id) => buildables.GetValueOrDefault(id) is { BuildableKind: BuildableKind.Station };
    }

    /// <summary>Whether an outline's middle is inside the area.</summary>
    private static bool Inside(List<(double X, double Y)> outline, WorldRectangle area)
    {
        double x = (outline.Min(point => point.X) + outline.Max(point => point.X)) / 2;
        double y = (outline.Min(point => point.Y) + outline.Max(point => point.Y)) / 2;
        return x >= area.X0 && x <= area.X1 && y >= area.Y0 && y <= area.Y1;
    }

    /// <summary>How long a way is, seen from above, in centimetres.</summary>
    private static double Length(IReadOnlyList<WorldPoint> way) =>
        way.Zip(way.Skip(1), (a, b) => Math.Abs(b.X - a.X) + Math.Abs(b.Y - a.Y)).Sum();

    /// <summary>Whether the rectangle around an outline reaches into an area at all.</summary>
    private static bool Touches(List<(double X, double Y)> outline, WorldRectangle area) =>
        outline.Min(point => point.X) <= area.X1 && outline.Max(point => point.X) >= area.X0
        && outline.Min(point => point.Y) <= area.Y1 && outline.Max(point => point.Y) >= area.Y0;

    private static WorldRectangle Around(List<(double X, double Y)> points) =>
        new(points.Min(point => point.X), points.Min(point => point.Y), points.Max(point => point.X), points.Max(point => point.Y));

    /// <summary>
    /// The points that outline a buildable seen from above, in centimetres: a belt's or pipe's curve, the corners of
    /// its footprint turned by its yaw, or else where it stands; none without a position.
    /// </summary>
    private static IEnumerable<(double X, double Y)> Outline(BuildableDocument buildable, Dictionary<string, FootprintDocument> footprints)
    {
        if (buildable.Curve is { Count: > 0 } curve)
        {
            return curve.Select(point => (point.Location.X, point.Location.Y));
        }

        if (buildable.Position is not { } position)
        {
            return [];
        }

        if (!footprints.TryGetValue(buildable.ClassName, out FootprintDocument? footprint))
        {
            return [(position.X, position.Y)];
        }

        double turn = (buildable.Yaw ?? 0) * Math.PI / 180;
        (double cos, double sin) = (Math.Cos(turn), Math.Sin(turn));
        (double X, double Y)[] corners =
        [
            ((double)footprint.MinX, (double)footprint.MinY), ((double)footprint.MaxX, (double)footprint.MinY),
            ((double)footprint.MaxX, (double)footprint.MaxY), ((double)footprint.MinX, (double)footprint.MaxY),
        ];
        return corners.Select(corner => (position.X + (corner.X * cos) - (corner.Y * sin), position.Y + (corner.X * sin) + (corner.Y * cos)));
    }

    /// <summary>The top belt's and the top pipe's capacity per minute among the tiers the snapshot knows.</summary>
    private static (double Belt, double Pipe) TopCapacities(SnapshotDocument snapshot)
    {
        double belt = snapshot.TransportTiers.Where(tier => tier.BuildableKind == BuildableKind.BeltOrLift).Select(tier => (double)tier.CapacityPerMinute).DefaultIfEmpty(1200).Max();
        double pipe = snapshot.TransportTiers.Where(tier => tier.BuildableKind == BuildableKind.Pipe).Select(tier => (double)tier.CapacityPerMinute).DefaultIfEmpty(600).Max();
        return (belt, pipe);
    }
}
