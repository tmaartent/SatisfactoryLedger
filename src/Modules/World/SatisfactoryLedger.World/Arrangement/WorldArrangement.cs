using SatisfactoryLedger.World.Analysis;
using SatisfactoryLedger.World.Contracts.Analysis;
using SatisfactoryLedger.World.Contracts.Arrangement;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Arrangement;
using SatisfactoryLedger.World.Factories;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Arrangement;

/// <summary>
/// The optimal arrangement's problem made from a world and its stored analysis (decided 2026-11-12):
/// <list type="bullet">
/// <item>The area: the rectangle drawn on the map (everything built when none is), divided into plots in rows and
/// columns with roads between them; a cell of the problem is a plot and its road.</item>
/// <item>The factories inside it (their machines' middle): each on one plot, where its machines' middle is (decided
/// 2026-11-18: every factory is built to fit a plot).</item>
/// <item>Their links: what inputs bring now (<see cref="FactoryInputs"/>, BR-A19) from another factory, to or from a
/// factory inside, each item weighing the belts (or, for a fluid, pipes) it needs: its rate over the top tier's
/// capacity, rounded up.
/// Inputs from nodes and stations don't count, as a first step (decided 2026-11-12).</item>
/// <item>Links with nodes' extractors, stations and drone ports (decided 2026-11-18): what they bring the arranged
/// factories, and what the factories send to stations and drone ports, at the weight of the settings; each building
/// at its spot on the belt floor, where its belts are routed to. A node doesn't block: factories may stand over it.
/// Extractors, stations and drone ports feeding a factory one item share belts up to the top belt's (or pipe's)
/// capacity, and so do stations and drone ports a factory sends one item to (<see cref="SupplyBelts"/>).</item>
/// <item>What stays: the other factories and every train or truck station and drone port, which keep the plots they
/// touch; and the exclusions drawn, whose plots are kept free.</item>
/// </list>
/// </summary>
internal static class WorldArrangement
{
    /// <summary>What stays where it is and takes its space: the stations' classes.</summary>
    private static readonly HashSet<string> Stations = new(
        ["Build_TrainDockingStation_C", "Build_TrainDockingStationLiquid_C", "Build_TruckStation_C", "Build_DroneStation_C"],
        StringComparer.Ordinal);

    /// <returns>The problem; its grid; where the spots stand, in squares; and per link what each of its belts and pipes
    /// carries, in the order the router numbers them.</returns>
    public static (
        ArrangementProblem Problem,
        ArrangementGrid Grid,
        IReadOnlyDictionary<string, GridRectangle> Spots,
        IReadOnlyDictionary<(string From, string To), IReadOnlyList<BeltLoadDocument>> Loads) Of(
        SnapshotDocument snapshot, AnalysisDocument analysis, IReadOnlyList<StoredFactory> factories, ArrangementSettings settings) =>
        Make(snapshot, analysis, factories, settings);

    /// <summary>
    /// Where things stand only: the factories to arrange and what blocks the plots, without links. Enough to tell whether
    /// the factories fit, in a second instead of the fifteen the links take on a large world (2026-11-18).
    /// </summary>
    public static ArrangementProblem Placing(SnapshotDocument snapshot, IReadOnlyList<StoredFactory> factories, ArrangementSettings settings) =>
        Make(snapshot, analysis: null, factories, settings).Problem;

    /// <param name="analysis">The flows that make the links; none without it.</param>
    private static (
        ArrangementProblem Problem,
        ArrangementGrid Grid,
        IReadOnlyDictionary<string, GridRectangle> Spots,
        IReadOnlyDictionary<(string From, string To), IReadOnlyList<BeltLoadDocument>> Loads) Make(
        SnapshotDocument snapshot, AnalysisDocument? analysis, IReadOnlyList<StoredFactory> factories, ArrangementSettings settings)
    {
        Dictionary<string, BuildableDocument> buildables = snapshot.Buildables.ToDictionary(buildable => buildable.Id, StringComparer.Ordinal);
        Dictionary<string, FootprintDocument> footprints = (snapshot.Footprints ?? []).ToDictionary(footprint => footprint.Id, StringComparer.Ordinal);
        WorldRectangle area = settings.Area ?? Around([.. snapshot.Buildables.SelectMany(buildable => Outline(buildable, footprints))]);
        ArrangementGrid grid = ArrangementGrid.Of(area.X0, area.Y0, settings);
        (int plotsX, int plotsY) = grid.PlotsIn(area.X1 - area.X0, area.Y1 - area.Y0);

        WorldGraph world = snapshot.ToWorldGraph();
        HashSet<ItemId> fluids = [.. snapshot.Fluids.Select(ItemId.Of)];
        (double belt, double pipe) = TopCapacities(snapshot);

        // Every factory traced, with the outline of its machines; those whose middle is inside the area are arranged.
        List<(StoredFactory Factory, FactoryMembers Members, List<(double X, double Y)> Outline)> traced = [];
        foreach (StoredFactory factory in factories)
        {
            FactoryDefinition? definition = factory.ToDefinition(MachineMatch.In(snapshot, factory.Machines));
            if (definition is null || !definition.IsTraceableIn(world))
            {
                continue;
            }

            FactoryMembers members = definition.TraceMembers(world, fluids);
            List<(double X, double Y)> outline = [.. members.Machines
                .Select(id => buildables.GetValueOrDefault(id.Id))
                .OfType<BuildableDocument>()
                .SelectMany(buildable => Outline(buildable, footprints))];
            if (outline.Count > 0)
            {
                traced.Add((factory, members, outline));
            }
        }

        HashSet<string> chosen = [.. traced.Where(entry => Inside(entry.Outline, area)).Select(entry => entry.Factory.Id.ToString())];
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
        void AddSpot(string id, IEnumerable<string> standing, string from, string to, string item, double perMinute)
        {
            List<(double X, double Y)> outline = [.. standing
                .Select(buildables.GetValueOrDefault)
                .OfType<BuildableDocument>()
                .SelectMany(building => Outline(building, footprints))];
            if (outline.Count == 0 || perMinute <= 0)
            {
                return;
            }

            spots.TryAdd(id, grid.SquaresTouching(outline, floor));

            // A station or drone port blocks its plots below; a node doesn't.
            fixedPlaces.TryAdd(id, new FixedPlace(id, grid.PlotsTouching(outline), Blocks: false));
            spotRates[(from, to, item)] = spotRates.GetValueOrDefault((from, to, item)) + perMinute;
        }

        // Per arranged factory and item, the extractors, stations and drone ports that feed it: merged onto belts afterwards.
        Dictionary<(string To, string Item), List<Supply>> supplies = [];
        foreach ((StoredFactory factory, FactoryMembers members, List<(double X, double Y)> outline) in traced)
        {
            string to = factory.Id.ToString();
            if (chosen.Contains(to))
            {
                arranged.Add(new ArrangedFactory(to, grid.PlotOf(outline)));
            }
            else
            {
                fixedPlaces[to] = new FixedPlace(to, grid.PlotsTouching(outline), Blocks: true);
            }

            foreach (FactoryInput input in analysis is null ? [] : FactoryInputs.Of(snapshot, world, factory, members, analysis, factories))
            {
                foreach (InputSource source in input.Sources)
                {
                    // A node's extractor, a station or a drone port feeding a factory being arranged: merged with the
                    // others that bring it the item.
                    if (chosen.Contains(to) && source is { Kind: InputSourceKind.Building, Id: { } building } && IsSpot(building)
                        && buildables.GetValueOrDefault(building)?.Position is { } position)
                    {
                        List<Supply> feeding = supplies.TryGetValue((to, input.Item), out List<Supply>? found) ? found : supplies[(to, input.Item)] = [];
                        feeding.Add(new Supply(building, position.X, position.Y, source.PerMinute));
                    }

                    string? from = source is { Kind: InputSourceKind.Factory, FactoryId: { } other } && other != factory.Id ? other.ToString() : null;

                    // Only links that touch a factory being arranged change with the arrangement.
                    if (from is null || !(chosen.Contains(to) || chosen.Contains(from)))
                    {
                        continue;
                    }

                    factoryRates[(from, to, input.Item)] = factoryRates.GetValueOrDefault((from, to, input.Item)) + source.PerMinute;
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
        foreach ((StoredFactory factory, FactoryMembers members, _) in traced.Where(entry => analysis is not null && chosen.Contains(entry.Factory.Id.ToString())))
        {
            string from = factory.Id.ToString();
            IEnumerable<IGrouping<ItemId, Supply>> sent = FactoryOutputs.Ends(world, members, analysis!)
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
                foreach (IReadOnlyList<Supply> merged in SupplyBelts.Merge(served, fluids.Contains(served.Key) ? pipe : belt))
                {
                    string id = merged.Count == 1 ? merged[0].Id : $"{from}>{merged[0].Id}+{merged.Count - 1}";
                    AddSpot(id, merged.Select(station => station.Id), from, id, served.Key.Id, merged.Sum(station => station.PerMinute));
                }
            }
        }

        // Every station keeps its plots, whether or not it feeds a factory.
        foreach (BuildableDocument station in snapshot.Buildables.Where(buildable => Stations.Contains(buildable.ClassName) && buildable.Position is not null))
        {
            fixedPlaces[station.Id] = new FixedPlace(station.Id, grid.PlotsTouching([.. Outline(station, footprints)]), Blocks: true);
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

        // The roads come with the plots: no gap besides them.
        var problem = new ArrangementProblem(
            arranged,
            [.. fixedPlaces.Values],
            [.. LinksOf(factoryRates, 100), .. LinksOf(spotRates, (int)Math.Round(settings.SpotWeight * 100))],
            new GridRectangle(0, 0, plotsX, plotsY),
            GapX: 0,
            GapY: 0,
            settings.Together);
        return (problem, grid, spots, loads);

        // A node's extractor, or a station or drone port: what a belt may go down to at its spot.
        bool IsSpot(string id) => world.Find(BuildableId.Of(id)) is Provider || IsStation(id);
        bool IsStation(string id) => buildables.GetValueOrDefault(id) is { } building && Stations.Contains(building.ClassName);
    }

    /// <summary>Whether an outline's middle is inside the area.</summary>
    private static bool Inside(List<(double X, double Y)> outline, WorldRectangle area)
    {
        double x = (outline.Min(point => point.X) + outline.Max(point => point.X)) / 2;
        double y = (outline.Min(point => point.Y) + outline.Max(point => point.Y)) / 2;
        return x >= area.X0 && x <= area.X1 && y >= area.Y0 && y <= area.Y1;
    }

    private static WorldRectangle Around(List<(double X, double Y)> points) =>
        new(points.Min(point => point.X), points.Min(point => point.Y), points.Max(point => point.X), points.Max(point => point.Y));

    /// <summary>The problem as the analysis service receives it, with how long the solver may take and the floor to route belts on.</summary>
    public static ArrangementProblemDocument ToDocument(
        ArrangementProblem problem,
        int seconds,
        BeltFloor floor,
        IReadOnlyDictionary<string, GridRectangle> spots,
        IReadOnlyDictionary<(string From, string To), IReadOnlyList<BeltLoadDocument>> loads) => new(
        [.. problem.Factories.Select(factory => new ArrangedFactoryDocument(factory.Id, Document(factory.Now)))],
        [.. problem.Fixed.Select(place => new FixedPlaceDocument(place.Id, Document(place.Space), place.Blocks))],
        [.. problem.Links.Select(link => new ArrangementLinkDocument(
            link.From, link.To, link.Belts, link.WeightPercent, link.Pipes, loads.GetValueOrDefault((link.From, link.To))))],
        Document(problem.Area),
        problem.GapX,
        problem.GapY,
        problem.Together,
        seconds,
        new BeltFloorDocument(floor.SquaresPerPlotX, floor.SquaresPerPlotY, floor.RoadSquaresX, floor.RoadSquaresY, floor.Levels),
        [.. spots.Select(spot => new SpotDocument(spot.Key, Document(spot.Value)))]);

    private static GridRectangleDocument Document(GridRectangle rectangle) => new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Depth);

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
        static bool IsPipe(TransportTierDocument tier) => tier.Id.Contains("Pipeline", StringComparison.Ordinal);
        double belt = snapshot.TransportTiers.Where(tier => !IsPipe(tier)).Select(tier => (double)tier.CapacityPerMinute).DefaultIfEmpty(1200).Max();
        double pipe = snapshot.TransportTiers.Where(IsPipe).Select(tier => (double)tier.CapacityPerMinute).DefaultIfEmpty(600).Max();
        return (belt, pipe);
    }
}
