using SatisfactoryLedger.World.Arrangement;
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

    private static (ArrangementProblem Problem, ArrangementGrid Grid) Arranged(WorldRectangle? area, WorldRectangle[] exclusions, params StoredFactory[] factories) =>
        WorldArrangement.Of(
            Document,
            TestWorldAnalysis,
            factories,
            new ArrangementSettings(area, PlotWidthMetres: 16, PlotDepthMetres: 16, RoadXMetres: 8, RoadYMetres: 8, Together: 0, exclusions));

    [Fact]
    public void A_factory_takes_the_whole_plots_around_its_machines()
    {
        StoredFactory ironPlates = Factory("Iron plates", SmelterA, SmelterB, ConstructorC);
        (ArrangementProblem problem, ArrangementGrid grid) = Arranged(null, ironPlates);

        GridRectangle plots = Assert.Single(problem.Factories).Now;
        foreach (string machine in new[] { SmelterA, SmelterB, ConstructorC })
        {
            (double x, double y) = grid.CellOf(PositionOf(machine).X, PositionOf(machine).Y);
            Assert.True(x >= plots.X && x <= plots.X + plots.Width && y >= plots.Y && y <= plots.Y + plots.Depth, $"{machine} at ({x}, {y}) outside {plots}");
        }
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
    public void Ore_from_a_miner_is_no_link_only_inputs_from_factories_count()
    {
        (ArrangementProblem problem, _) = Arranged(null, Smelting, Plates);

        Assert.DoesNotContain(problem.Links, link => link.From == Miner);
        Assert.DoesNotContain(problem.Fixed, place => place.Id == Miner);
    }

    [Fact]
    public void Ingots_from_another_factory_are_a_link_weighing_the_belts_they_need()
    {
        (ArrangementProblem problem, _) = Arranged(null, Smelting, Plates);

        // 12.5 ingots a minute fit on one belt.
        Assert.Contains(new ArrangementLink(Smelting.Id.ToString(), Plates.Id.ToString(), Belts: 1), problem.Links);
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
