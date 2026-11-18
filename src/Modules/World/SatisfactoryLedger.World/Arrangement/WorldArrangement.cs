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
/// <item>The factories inside it (their machines' middle): each on as many whole plots as its machines and generators
/// need (their footprints, turned by their yaw), not its belts and pipes, whose supply lines the arrangement shortens.</item>
/// <item>Their links: what inputs bring now (<see cref="FactoryInputs"/>, BR-A19) from another factory, to or from a
/// factory inside, each item weighing the belts (or pipes) it needs: its rate over the top tier's capacity, rounded up.
/// Inputs from nodes and stations don't count, as a first step (decided 2026-11-12).</item>
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

    public static (ArrangementProblem Problem, ArrangementGrid Grid) Of(
        SnapshotDocument snapshot, AnalysisDocument analysis, IReadOnlyList<StoredFactory> factories, ArrangementSettings settings)
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
        Dictionary<(string From, string To), int> belts = [];
        foreach ((StoredFactory factory, FactoryMembers members, List<(double X, double Y)> outline) in traced)
        {
            string to = factory.Id.ToString();
            if (chosen.Contains(to))
            {
                arranged.Add(new ArrangedFactory(to, grid.PlotsFor(outline)));
            }
            else
            {
                fixedPlaces[to] = new FixedPlace(to, grid.PlotsTouching(outline), Blocks: true);
            }

            foreach (FactoryInput input in FactoryInputs.Of(snapshot, world, factory, members, analysis, factories))
            {
                double capacity = fluids.Contains(ItemId.Of(input.Item)) ? pipe : belt;
                foreach (InputSource source in input.Sources)
                {
                    string? from = source is { Kind: InputSourceKind.Factory, FactoryId: { } other } && other != factory.Id ? other.ToString() : null;

                    // Only links that touch a factory being arranged change with the arrangement.
                    if (from is null || !(chosen.Contains(to) || chosen.Contains(from)))
                    {
                        continue;
                    }

                    belts[(from, to)] = belts.GetValueOrDefault((from, to)) + (int)Math.Ceiling(source.PerMinute / capacity);
                }
            }
        }

        // Every station keeps its plots, whether or not it feeds a factory.
        foreach (BuildableDocument station in snapshot.Buildables.Where(buildable => Stations.Contains(buildable.ClassName) && buildable.Position is not null))
        {
            fixedPlaces.TryAdd(station.Id, new FixedPlace(station.Id, grid.PlotsTouching([.. Outline(station, footprints)]), Blocks: true));
        }

        // Where the player keeps factories out, as places that stay and block.
        foreach ((WorldRectangle exclusion, int index) in settings.Exclusions.Select((exclusion, index) => (exclusion, index)))
        {
            string id = $"exclusion.{index}";
            fixedPlaces[id] = new FixedPlace(id, grid.PlotsTouching([(exclusion.X0, exclusion.Y0), (exclusion.X1, exclusion.Y1)]), Blocks: true);
        }

        // The roads come with the plots: no gap besides them.
        var problem = new ArrangementProblem(
            arranged,
            [.. fixedPlaces.Values],
            [.. belts.Where(link => link.Value > 0).Select(link => new ArrangementLink(link.Key.From, link.Key.To, link.Value))],
            new GridRectangle(0, 0, plotsX, plotsY),
            GapX: 0,
            GapY: 0,
            settings.Together);
        return (problem, grid);
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

    /// <summary>The problem as the analysis service receives it, with how long the solver may take.</summary>
    public static ArrangementProblemDocument ToDocument(ArrangementProblem problem, int seconds) => new(
        [.. problem.Factories.Select(factory => new ArrangedFactoryDocument(factory.Id, Document(factory.Now)))],
        [.. problem.Fixed.Select(place => new FixedPlaceDocument(place.Id, Document(place.Space), place.Blocks))],
        [.. problem.Links.Select(link => new ArrangementLinkDocument(link.From, link.To, link.Belts))],
        Document(problem.Area),
        problem.GapX,
        problem.GapY,
        problem.Together,
        seconds);

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
