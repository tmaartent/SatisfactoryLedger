using SatisfactoryLedger.World.Domain.Arrangement;

namespace SatisfactoryLedger.World.Domain.Tests.Arrangement;

/// <summary>
/// The optimal arrangement's problem (decided 2026-11-12): factories as rectangles on a grid, stations and resource
/// nodes as fixed ones, and what a layout costs: per link, the belts it needs times the distance along the grid between
/// the two centres.
/// </summary>
public class ArrangementProblemTests
{
    private static ArrangementProblem Problem(params ArrangementLink[] links) => new(
        [new ArrangedFactory("smelting", new GridRectangle(0, 0, 4, 2)), new ArrangedFactory("plates", new GridRectangle(10, 0, 2, 2))],
        [new FixedPlace("miner", new GridRectangle(0, 10, 2, 2), Blocks: false)],
        links,
        Area: new GridRectangle(0, 0, 100, 100),
        GapX: 1,
        GapY: 1,
        Together: 0);

    [Fact]
    public void A_link_costs_the_belts_it_needs_times_the_grid_distance_between_the_centres()
    {
        // Centres (2, 1) and (11, 1): 9 cells apart; two belts.
        ArrangementProblem problem = Problem(new ArrangementLink("smelting", "plates", Belts: 2));

        Assert.Equal(18, problem.CostNow);
    }

    [Fact]
    public void A_link_to_a_fixed_place_counts_too()
    {
        // Centres (1, 11) and (2, 1): 1 + 10 cells apart; one belt.
        ArrangementProblem problem = Problem(new ArrangementLink("miner", "smelting", Belts: 1));

        Assert.Equal(11, problem.CostNow);
    }

    [Fact]
    public void A_layout_costs_what_its_links_cost_in_their_places_there()
    {
        ArrangementProblem problem = Problem(new ArrangementLink("smelting", "plates", Belts: 1));

        double cost = problem.CostOf(new Dictionary<string, GridRectangle>
        {
            ["smelting"] = new(0, 0, 4, 2),
            ["plates"] = new(5, 0, 2, 2),
        });

        // Centres (2, 1) and (6, 1).
        Assert.Equal(4, cost);
    }

    [Fact]
    public void A_link_to_a_node_or_station_counts_at_its_weight()
    {
        // Centres (1, 11) and (2, 1): 11 cells apart; one belt, weighed at half (decided 2026-11-18).
        ArrangementProblem problem = Problem(new ArrangementLink("miner", "smelting", 1, WeightPercent: 50));

        Assert.Equal(5.5, problem.CostNow);
    }

    [Fact]
    public void A_link_s_pipes_count_as_its_belts_do()
    {
        // Centres (2, 1) and (11, 1): 9 cells apart; a belt and a pipe.
        ArrangementProblem problem = Problem(new ArrangementLink("smelting", "plates", Belts: 1, Pipes: 1));

        Assert.Equal(18, problem.CostNow);
    }

    [Fact]
    public void The_free_plots_are_the_area_less_what_blocks_it()
    {
        // 10 by 10; a station on 2 by 2, an exclusion overlapping it by one plot, a node that doesn't block, and a
        // factory that stays reaching one plot into the area.
        var problem = new ArrangementProblem(
            [new ArrangedFactory("smelting", new GridRectangle(4, 4, 1, 1))],
            [
                new FixedPlace("station", new GridRectangle(0, 0, 2, 2), Blocks: true),
                new FixedPlace("exclusion.0", new GridRectangle(1, 1, 2, 2), Blocks: true),
                new FixedPlace("miner", new GridRectangle(5, 5, 1, 1), Blocks: false),
                new FixedPlace("outside", new GridRectangle(9, 9, 3, 3), Blocks: true),
            ],
            [],
            Area: new GridRectangle(0, 0, 10, 10),
            GapX: 0,
            GapY: 0,
            Together: 0);

        Assert.Equal(100 - 4 - 3 - 1, problem.FreePlots);
    }

    [Fact]
    public void Factories_have_no_room_when_there_are_fewer_free_plots_than_they_take()
    {
        var problem = new ArrangementProblem(
            [new ArrangedFactory("smelting", new GridRectangle(0, 0, 1, 1)), new ArrangedFactory("plates", new GridRectangle(1, 0, 1, 1))],
            [new FixedPlace("station", new GridRectangle(1, 0, 1, 1), Blocks: true)],
            [],
            Area: new GridRectangle(0, 0, 2, 1),
            GapX: 0,
            GapY: 0,
            Together: 0);

        Assert.False(problem.HasRoom);
    }

    [Fact]
    public void A_factory_needs_room()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GridRectangle(0, 0, 0, 2));
    }
}
