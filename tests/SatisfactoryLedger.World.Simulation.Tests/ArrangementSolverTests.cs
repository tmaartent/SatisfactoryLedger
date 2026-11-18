using SatisfactoryLedger.World.Domain.Arrangement;

namespace SatisfactoryLedger.World.Simulation.Tests;

/// <summary>
/// The optimal arrangement (decided 2026-11-12), solved with OR-Tools' CP-SAT: factories placed so the belt work between
/// them and their fixed inputs is least, without overlapping, with gaps between them, inside the area.
/// </summary>
public class ArrangementSolverTests
{
    private static readonly GridRectangle World = new(0, 0, 60, 60);

    private static ArrangementProposal Solve(
        ArrangedFactory[] factories, FixedPlace[] fixedPlaces, ArrangementLink[] links, int gapX = 1, int gapY = 1, int together = 0) =>
        ArrangementSolver.Solve(new ArrangementProblem(factories, fixedPlaces, links, World, gapX, gapY, together), TimeSpan.FromSeconds(10));

    /// <summary>Whether two rectangles keep the gaps between them: apart along x by gapX, or along y by gapY.</summary>
    private static bool Apart(GridRectangle a, GridRectangle b, int gapX, int gapY) =>
        a.X + a.Width + gapX <= b.X || b.X + b.Width + gapX <= a.X || a.Y + a.Depth + gapY <= b.Y || b.Y + b.Depth + gapY <= a.Y;

    [Fact]
    public void Two_linked_factories_far_apart_end_up_side_by_side()
    {
        ArrangementProposal proposal = Solve(
            [new ArrangedFactory("smelting", new GridRectangle(0, 0, 4, 2)), new ArrangedFactory("plates", new GridRectangle(50, 50, 2, 2))],
            [],
            [new ArrangementLink("smelting", "plates", Belts: 2)]);

        // Side by side along y: half of 2, the gap of 1, half of 2: 3 cells between the centres, two belts.
        Assert.Equal((ArrangementStatus.Optimal, 6.0), (proposal.Status, proposal.Cost));
    }

    [Fact]
    public void Factories_never_overlap_and_keep_their_gaps()
    {
        ArrangementProposal proposal = Solve(
            [new ArrangedFactory("a", new GridRectangle(0, 0, 5, 5)), new ArrangedFactory("b", new GridRectangle(40, 0, 5, 5)), new ArrangedFactory("c", new GridRectangle(0, 40, 5, 5))],
            [new FixedPlace("miner", new GridRectangle(30, 30, 2, 2), Blocks: false)],
            [new ArrangementLink("miner", "a", 1), new ArrangementLink("miner", "b", 1), new ArrangementLink("miner", "c", 1)],
            gapX: 2,
            gapY: 3);

        GridRectangle[] places = [.. proposal.Places.Values];
        for (int i = 0; i < places.Length; i++)
        {
            for (int j = i + 1; j < places.Length; j++)
            {
                Assert.True(Apart(places[i], places[j], 2, 3), $"{places[i]} and {places[j]}");
            }
        }
    }

    [Fact]
    public void A_factory_is_drawn_next_to_the_station_that_feeds_it_but_never_onto_it()
    {
        GridRectangle station = new(30, 30, 2, 2);

        ArrangementProposal proposal = Solve(
            [new ArrangedFactory("smelting", new GridRectangle(0, 0, 4, 4))],
            [new FixedPlace("station", station, Blocks: true)],
            [new ArrangementLink("station", "smelting", 1)]);

        Assert.True(Apart(proposal.Places["smelting"], station, 1, 1));
        // Half of 4, the gap of 1, half of 2: 4 cells between the centres.
        Assert.Equal(4, proposal.Cost);
    }

    [Fact]
    public void A_factory_may_stand_over_the_node_that_feeds_it_on_platforms_above_its_extractors()
    {
        ArrangementProposal proposal = Solve(
            [new ArrangedFactory("smelting", new GridRectangle(0, 0, 4, 4))],
            [new FixedPlace("node", new GridRectangle(30, 30, 2, 2), Blocks: false)],
            [new ArrangementLink("node", "smelting", 1)]);

        Assert.Equal(0, proposal.Cost);
    }

    [Fact]
    public void Factories_stay_inside_the_area()
    {
        ArrangementProposal proposal = Solve(
            [new ArrangedFactory("smelting", new GridRectangle(10, 10, 6, 6))],
            [new FixedPlace("miner", new GridRectangle(0, 0, 2, 2), Blocks: false)],
            [new ArrangementLink("miner", "smelting", 1)]);

        GridRectangle place = proposal.Places["smelting"];
        Assert.True(place.X >= 0 && place.Y >= 0 && place.X + place.Width <= 60 && place.Y + place.Depth <= 60, $"{place}");
    }

    [Fact]
    public void A_proposal_never_costs_more_than_the_layout_now()
    {
        ArrangementProposal proposal = Solve(
            [new ArrangedFactory("a", new GridRectangle(0, 0, 3, 3)), new ArrangedFactory("b", new GridRectangle(4, 0, 3, 3))],
            [],
            [new ArrangementLink("a", "b", 3)]);

        Assert.True(proposal.Cost <= proposal.CostNow, $"{proposal.Cost} > {proposal.CostNow}");
    }

    [Fact]
    public void A_factory_that_saves_no_belt_work_by_moving_stays_where_it_is()
    {
        ArrangementProposal proposal = Solve(
            [new ArrangedFactory("smelting", new GridRectangle(0, 0, 4, 2)), new ArrangedFactory("plates", new GridRectangle(20, 0, 2, 2)), new ArrangedFactory("alone", new GridRectangle(40, 40, 3, 3))],
            [],
            [new ArrangementLink("smelting", "plates", 1)]);

        Assert.Equal(new GridRectangle(40, 40, 3, 3), proposal.Places["alone"]);
    }

    [Fact]
    public void Factories_kept_together_stand_side_by_side_though_their_inputs_are_far_apart()
    {
        ArrangementProposal proposal = Solve(
            [new ArrangedFactory("smelting", new GridRectangle(0, 0, 2, 2)), new ArrangedFactory("refinery", new GridRectangle(58, 58, 2, 2))],
            [new FixedPlace("iron", new GridRectangle(0, 0, 2, 2), Blocks: false), new FixedPlace("oil", new GridRectangle(58, 58, 2, 2), Blocks: false)],
            [new ArrangementLink("iron", "smelting", 1), new ArrangementLink("oil", "refinery", 1)],
            together: 2);

        // Each on its own node, the two would span the whole area: 60 + 60 cells, 240 at a weight of 2. Side by side, 5 + 2.
        GridRectangle smelting = proposal.Places["smelting"];
        GridRectangle refinery = proposal.Places["refinery"];
        Assert.Equal(3, Math.Abs(smelting.X - refinery.X) + Math.Abs(smelting.Y - refinery.Y));
    }
}
