using SatisfactoryLedger.World.Arrangement;
using SatisfactoryLedger.World.Contracts.Arrangement;
using SatisfactoryLedger.World.Domain.Arrangement;
using SatisfactoryLedger.World.Factories;
using SatisfactoryLedger.World.Snapshots;

using static SatisfactoryLedger.World.Tests.TestWorldSnapshot;

namespace SatisfactoryLedger.World.Tests;

/// <summary>
/// The optimal arrangement's problem made from a world (decided 2026-11-12), in the test world: the drawn area divided
/// into plots with roads between them, the factories inside it on whole plots, their inputs from other factories as links
/// weighted by the belts they need, and what stays put (other factories, stations) as fixed places.
/// </summary>
public class WorldArrangementTests
{
    private static StoredFactory Factory(string name, params string[] machines) => new()
    {
        Id = Guid.CreateVersion7(),
        World = "Development",
        Name = name,
        Machines = [.. machines.Select(id => StoredMachine.From(Document.Buildables.Single(buildable => buildable.Id == id)))],
    };

    private static readonly StoredFactory Smelting = Factory("Smelting", SmelterA);
    private static readonly StoredFactory Plates = Factory("Plates", ConstructorC);

    private static PositionDocument PositionOf(string id) => Document.Buildables.Single(buildable => buildable.Id == id).Position!;

    /// <summary>Plots of 16 by 16 metres with roads of 8; the area everything built, unless given.</summary>
    private static (ArrangementProblem Problem, ArrangementGrid Grid) Arranged(WorldRectangle? area, params StoredFactory[] factories) =>
        Arranged(area, [], factories);

    private static (ArrangementProblem Problem, ArrangementGrid Grid) Arranged(WorldRectangle? area, WorldRectangle[] exclusions, params StoredFactory[] factories)
    {
        MadeArrangement made = Made(area, exclusions, factories);
        return (made.Problem, made.Grid);
    }

    private static MadeArrangement Made(WorldRectangle? area, WorldRectangle[] exclusions, params StoredFactory[] factories) =>
        Made(area, exclusions, supplyLines: false, factories);

    private static MadeArrangement Made(WorldRectangle? area, WorldRectangle[] exclusions, bool supplyLines, params StoredFactory[] factories) =>
        WorldArrangement.Of(
            Document,
            TestWorldAnalysis,
            factories,
            new ArrangementSettings(area, PlotWidthMetres: 16, PlotDepthMetres: 16, RoadXMetres: 8, RoadYMetres: 8, Together: 0, SpotWeight: 0.5, CeilingMetres: 32, exclusions, supplyLines));

    [Fact]
    public void Every_factory_takes_one_plot_where_its_machines_stand_however_far_they_spread()
    {
        // Plots of 16 metres: constructor C stands 21 metres from smelter A, yet the factory takes one plot (decided 2026-11-18).
        StoredFactory ironPlates = Factory("Iron plates", SmelterA, SmelterB, ConstructorC);
        (ArrangementProblem problem, ArrangementGrid grid) = Arranged(null, ironPlates);

        GridRectangle plot = Assert.Single(problem.Factories).Now;
        (double x, double y) = grid.CellOf(
            new[] { SmelterA, SmelterB, ConstructorC }.Average(machine => PositionOf(machine).X),
            new[] { SmelterA, SmelterB, ConstructorC }.Average(machine => PositionOf(machine).Y));
        Assert.Equal((1, 1), (plot.Width, plot.Depth));
        Assert.True(Math.Abs(x - (plot.X + 0.5)) <= 1 && Math.Abs(y - (plot.Y + 0.5)) <= 1, $"middle ({x}, {y}) far from {plot}");
    }

    /// <summary>Factories on their footprints, with roads of 8 metres (one foundation) along x and 16 (two) along y.</summary>
    private static MadeArrangement OnFootprints(params StoredFactory[] factories) =>
        WorldArrangement.Of(
            Document,
            TestWorldAnalysis,
            factories,
            new ArrangementSettings(null, PlotWidthMetres: 16, PlotDepthMetres: 16, RoadXMetres: 8, RoadYMetres: 16, Together: 0, SpotWeight: 0.5, CeilingMetres: 32, [], Footprints: true));

    [Fact]
    public void On_footprints_a_factory_takes_the_whole_foundations_its_machines_cover()
    {
        // Constructor C stands 21 metres from smelter A: more than one foundation of 8 metres, whatever the plots.
        StoredFactory ironPlates = Factory("Iron plates", SmelterA, SmelterB, ConstructorC);
        MadeArrangement made = OnFootprints(ironPlates);

        GridRectangle footprint = Assert.Single(made.Problem.Factories).Now;
        WorldRectangle ground = made.Grid.WorldOf(new GridRectangleDocument(footprint.X, footprint.Y, footprint.Width, footprint.Depth));
        Assert.Equal((800.0, 800.0), (made.Grid.PitchX, made.Grid.PitchY));
        Assert.True(footprint.Width * footprint.Depth > 1, $"{footprint} is one foundation");
        Assert.All([SmelterA, SmelterB, ConstructorC], machine =>
        {
            PositionDocument at = PositionOf(machine);
            Assert.True(at.X > ground.X0 && at.X < ground.X1 && at.Y > ground.Y0 && at.Y < ground.Y1, $"{machine} at ({at.X}, {at.Y}) outside {ground}");
        });
    }

    [Fact]
    public void On_footprints_the_roads_are_the_gaps_between_factories_in_foundations()
    {
        ArrangementProblem problem = OnFootprints(Smelting, Plates).Problem;

        Assert.Equal((1, 2), (problem.GapX, problem.GapY));
    }

    [Fact]
    public void On_footprints_the_belt_floor_is_one_square_a_foundation_with_no_roads_of_its_own()
    {
        var settings = new ArrangementSettings(null, PlotWidthMetres: 48, PlotDepthMetres: 48, RoadXMetres: 32, RoadYMetres: 32, Together: 0, SpotWeight: 0.5, CeilingMetres: 32, [], Footprints: true);

        Assert.Equal(new BeltFloor(1, 1, 0, 0, 16), settings.Floor);
    }

    [Fact]
    public void Only_the_factories_inside_the_area_are_arranged_the_others_stay_and_take_their_plots()
    {
        // An area of 20 metres around smelter A: constructor C stands 21 metres west of it.
        PositionDocument smelter = PositionOf(SmelterA);
        var around = new WorldRectangle(smelter.X - 1000, smelter.Y - 1000, smelter.X + 1000, smelter.Y + 1000);

        (ArrangementProblem problem, _) = Arranged(around, Smelting, Plates);

        Assert.Equal([Smelting.Id.ToString()], problem.Factories.Select(factory => factory.Id));
        Assert.Contains(problem.Fixed, place => place.Id == Plates.Id.ToString());
    }

    [Fact]
    public void Ore_from_a_miner_is_a_link_at_the_weight_of_nodes_and_stations_from_where_the_miner_stands()
    {
        (ArrangementProblem problem, _, IReadOnlyDictionary<string, GridRectangle> spots, _, _, _) = Made(null, [], Smelting);

        Assert.Contains(new ArrangementLink(Miner, Smelting.Id.ToString(), Belts: 1, WeightPercent: 50), problem.Links);
        Assert.Contains(problem.Fixed, place => place.Id == Miner && !place.Blocks);
        Assert.True(spots.ContainsKey(Miner));
    }

    [Fact]
    public void Ingots_from_another_factory_are_a_link_weighing_the_belts_they_need()
    {
        (ArrangementProblem problem, _) = Arranged(null, Smelting, Plates);

        // 12.5 ingots a minute fit on one belt.
        Assert.Contains(new ArrangementLink(Smelting.Id.ToString(), Plates.Id.ToString(), Belts: 1), problem.Links);
    }

    [Fact]
    public void Each_belt_of_a_link_carries_its_item_at_its_rate()
    {
        IReadOnlyDictionary<(string From, string To), IReadOnlyList<BeltLoadDocument>> loads = Made(null, [], Smelting, Plates).Loads;

        Assert.Equal([new BeltLoadDocument("Desc_IronIngot_C", 12.5, Pipe: false)], loads[(Smelting.Id.ToString(), Plates.Id.ToString())]);
    }

    [Fact]
    public void With_supply_lines_each_item_goes_with_what_brings_it_and_what_takes_it()
    {
        MadeArrangement made = Made(null, [], supplyLines: true, Smelting, Plates);

        ItemSupplyDocument ingots = Assert.Single(made.Supplies!, supply => supply.Item == "Desc_IronIngot_C");
        Assert.Equal([new SupplyEndDocument(Smelting.Id.ToString(), 12.5)], ingots.Sources);
        Assert.Equal([new SupplyEndDocument(Plates.Id.ToString(), 12.5)], ingots.Consumers);
        Assert.Equal((false, 1200.0), (ingots.Pipe, ingots.Capacity));
    }

    [Fact]
    public void With_supply_lines_the_extractors_of_an_item_bring_it_from_their_spots()
    {
        MadeArrangement made = Made(null, [], supplyLines: true, Smelting);

        ItemSupplyDocument ore = Assert.Single(made.Supplies!, supply => supply.Item == "Desc_OreIron_C");
        SupplyEndDocument miner = Assert.Single(ore.Sources);
        Assert.True(made.Spots.ContainsKey(miner.Id));
        Assert.Equal([Smelting.Id.ToString()], ore.Consumers.Select(consumer => consumer.Id));
        Assert.Equal(miner.PerMinute, ore.Consumers.Sum(consumer => consumer.PerMinute), 6);
    }

    [Fact]
    public void Without_supply_lines_the_links_are_routed()
    {
        Assert.Null(Made(null, [], Smelting, Plates).Supplies);
    }

    [Fact]
    public void A_miner_outside_the_area_is_reached_where_its_existing_belt_comes_into_the_area()
    {
        // An area from halfway between the miner and smelter A on, away from the miner: the miner is outside it.
        (PositionDocument miner, PositionDocument smelter) = (PositionOf(Miner), PositionOf(SmelterA));
        (double dx, double dy) = (smelter.X - miner.X, smelter.Y - miner.Y);
        WorldRectangle area = Math.Abs(dx) >= Math.Abs(dy)
            ? Span((miner.X + smelter.X) / 2, Math.Sign(dx) * 5000, smelter.Y - 5000, smelter.Y + 5000, alongX: true)
            : Span((miner.Y + smelter.Y) / 2, Math.Sign(dy) * 5000, smelter.X - 5000, smelter.X + 5000, alongX: false);

        MadeArrangement made = Made(area, [], Smelting);

        SpotEntry entry = Assert.Single(made.Entries).Value;

        // From where the miner's belt starts, by its output.
        BuildableDocument belt = Document.Buildables.Single(buildable => Document.Connections.Any(connection => connection.From == Miner && connection.To == buildable.Id));
        Assert.Equal(new WorldPoint(belt.Curve![0].Location.X, belt.Curve[0].Location.Y, belt.Curve[0].Location.Z), entry.Way[0]);
        Assert.True(Math.Abs(dx) >= Math.Abs(dy)
            ? Math.Abs(entry.Way[^1].X - ((miner.X + smelter.X) / 2)) < 1
            : Math.Abs(entry.Way[^1].Y - ((miner.Y + smelter.Y) / 2)) < 1, $"{entry.Way[^1]} isn't on the area's edge");

        static WorldRectangle Span(double from, double reach, double low, double high, bool alongX) =>
            alongX
                ? new WorldRectangle(Math.Min(from, from + reach), low, Math.Max(from, from + reach), high)
                : new WorldRectangle(low, Math.Min(from, from + reach), high, Math.Max(from, from + reach));
    }

    [Fact]
    public void A_miner_on_the_areas_edge_is_reached_where_it_stands_not_along_its_existing_belt()
    {
        // The area's edge runs through the miner, a metre past the middle of the ground it covers, away from smelter A
        // (2026-11-18: a train station on the edge was taken for one outside, and its belt drawn along the save's belts
        // round the outside). The miner as big as the game's: 6 metres across, from 5 behind it to 9 in front.
        BuildableDocument miner = Document.Buildables.Single(buildable => buildable.Id == Miner);
        PositionDocument smelter = PositionOf(SmelterA);
        double turn = (miner.Yaw ?? 0) * Math.PI / 180;
        double[] xs = [.. new[] { (-300.0, -500.0), (300.0, -500.0), (300.0, 900.0), (-300.0, 900.0) }
            .Select(corner => miner.Position!.X + (corner.Item1 * Math.Cos(turn)) - (corner.Item2 * Math.Sin(turn)))];
        double middle = (xs.Min() + xs.Max()) / 2;
        WorldRectangle area = smelter.X >= middle
            ? new WorldRectangle(middle + 100, smelter.Y - 50000, smelter.X + 50000, smelter.Y + 50000)
            : new WorldRectangle(smelter.X - 50000, smelter.Y - 50000, middle - 100, smelter.Y + 50000);
        SnapshotDocument withFootprint = Document with { Footprints = [new FootprintDocument("Build_MinerMk1_C", -300, -500, 300, 900)] };

        MadeArrangement made = WorldArrangement.Of(
            withFootprint,
            TestWorldAnalysis,
            [Smelting],
            new ArrangementSettings(area, PlotWidthMetres: 16, PlotDepthMetres: 16, RoadXMetres: 8, RoadYMetres: 8, Together: 0, SpotWeight: 0.5, CeilingMetres: 32, []));

        Assert.True(made.Spots.ContainsKey(Miner));
        Assert.False(made.Entries.ContainsKey(Miner));
    }

    [Fact]
    public void A_miner_on_the_areas_edge_but_off_the_belt_floor_is_reached_along_its_existing_belt()
    {
        // 2026-11-19: a miner straddled the area's east edge, past the last whole plot, so off the belt floor; its belt
        // left the floor 280 metres from it, and nothing showed the rest. Here the area's far edge runs through the
        // miner's middle, and the floor (20 plots of 16 metres with roads of 8) stops 15 metres short of it.
        (PositionDocument miner, PositionDocument smelter) = (PositionOf(Miner), PositionOf(SmelterA));
        bool alongX = miner.X - smelter.X >= miner.Y - smelter.Y;
        (double far, double near) = alongX ? (miner.X, smelter.X) : (miner.Y, smelter.Y);
        Assert.True(far - near > 2000, "the test needs the miner beyond smelter A along x or y");
        double start = far - (20 * 2400) - 1500;
        WorldRectangle area = alongX
            ? new WorldRectangle(start, smelter.Y - 50000, far, smelter.Y + 50000)
            : new WorldRectangle(smelter.X - 50000, start, smelter.X + 50000, far);
        SnapshotDocument withFootprint = Document with { Footprints = [new FootprintDocument("Build_MinerMk1_C", -300, -500, 300, 900)] };

        MadeArrangement made = WorldArrangement.Of(
            withFootprint,
            TestWorldAnalysis,
            [Smelting],
            new ArrangementSettings(area, PlotWidthMetres: 16, PlotDepthMetres: 16, RoadXMetres: 8, RoadYMetres: 8, Together: 0, SpotWeight: 0.5, CeilingMetres: 32, []));

        // It comes in where the existing belt reaches the floor's edge, 15 metres inside the area's.
        SpotEntry entry = made.Entries[Miner];
        Assert.Equal(far - 1500, alongX ? entry.Way[^1].X : entry.Way[^1].Y, 0);
    }

    [Fact]
    public void Where_things_stand_is_the_problem_without_its_links()
    {
        ArrangementProblem whole = Made(null, [], Smelting, Plates).Problem;

        ArrangementProblem placing = WorldArrangement.Placing(
            Document, [Smelting, Plates], new ArrangementSettings(null, PlotWidthMetres: 16, PlotDepthMetres: 16, RoadXMetres: 8, RoadYMetres: 8, Together: 0, SpotWeight: 0.5, CeilingMetres: 32, []));

        Assert.Equal(whole.Factories, placing.Factories);
        Assert.Equal(whole.Fixed.Where(place => place.Blocks), placing.Fixed.Where(place => place.Blocks));
        Assert.Equal((whole.Area, 0), (placing.Area, placing.Links.Count));
    }

    [Fact]
    public void A_freight_platform_in_the_area_blocks_its_plots()
    {
        (ArrangementProblem problem, _) = Arranged(null, Smelting);

        Assert.Contains(problem.Fixed, place => place.Id.Contains("Build_TrainDockingStation_C", StringComparison.Ordinal) && place.Blocks);
    }

    [Fact]
    public void An_exclusion_keeps_factories_off_the_plots_it_touches()
    {
        // A plot with its road is 24 metres: an exclusion of 19 metres from the area's corner touches the first plot only.
        PositionDocument smelter = PositionOf(SmelterA);
        var area = new WorldRectangle(smelter.X - 5000, smelter.Y - 5000, smelter.X + 5000, smelter.Y + 5000);
        var trackside = new WorldRectangle(area.X0 + 100, area.Y0 + 100, area.X0 + 2000, area.Y0 + 2000);

        (ArrangementProblem problem, _) = Arranged(area, [trackside], Smelting);

        Assert.Contains(new FixedPlace("exclusion.0", new GridRectangle(0, 0, 1, 1), Blocks: true), problem.Fixed);
    }

    [Fact]
    public void An_area_holds_as_many_plots_as_fit_with_the_roads_between_them()
    {
        // 100 metres: four plots of 16 with three roads of 8 take 88; a fifth would need 112.
        PositionDocument smelter = PositionOf(SmelterA);
        var area = new WorldRectangle(smelter.X - 5000, smelter.Y - 5000, smelter.X + 5000, smelter.Y + 5000);

        (ArrangementProblem problem, _) = Arranged(area, Smelting);

        Assert.Equal((4, 4, 0, 0), (problem.Area.Width, problem.Area.Depth, problem.GapX, problem.GapY));
    }
}
