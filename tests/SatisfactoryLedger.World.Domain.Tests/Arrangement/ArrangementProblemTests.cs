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
    public void A_factory_needs_room()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GridRectangle(0, 0, 0, 2));
    }
}
