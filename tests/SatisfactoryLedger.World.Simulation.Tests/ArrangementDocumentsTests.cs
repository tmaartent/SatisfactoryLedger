using SatisfactoryLedger.World.Contracts.Arrangement;

namespace SatisfactoryLedger.World.Simulation.Tests;

/// <summary>What the analysis service does with an arrangement request: solve the problem document into a proposal one.</summary>
public class ArrangementDocumentsTests
{
    [Fact]
    public void A_problem_document_is_solved_into_a_proposal_with_a_place_for_every_factory()
    {
        var problem = new ArrangementProblemDocument(
            [new ArrangedFactoryDocument("smelting", new GridRectangleDocument(0, 0, 4, 2)), new ArrangedFactoryDocument("plates", new GridRectangleDocument(50, 50, 2, 2))],
            [new FixedPlaceDocument("node", new GridRectangleDocument(10, 10, 2, 2), Blocks: false)],
            [new ArrangementLinkDocument("smelting", "plates", 2), new ArrangementLinkDocument("node", "smelting", 1)],
            new GridRectangleDocument(0, 0, 60, 60),
            GapX: 1,
            GapY: 1,
            Together: 0,
            Seconds: 5);

        ArrangementProposalDocument proposal = ArrangementDocuments.Solve(problem);

        Assert.Equal(["plates", "smelting"], proposal.Places.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(ArrangementQuality.Optimal, proposal.Quality);
        Assert.True(proposal.Cost < proposal.CostNow, $"{proposal.Cost} >= {proposal.CostNow}");
    }

    [Fact]
    public void The_belts_between_the_placed_factories_are_routed_on_the_floor_below_them()
    {
        var problem = new ArrangementProblemDocument(
            [new ArrangedFactoryDocument("smelting", new GridRectangleDocument(0, 0, 1, 1)), new ArrangedFactoryDocument("plates", new GridRectangleDocument(5, 5, 1, 1))],
            [],
            [new ArrangementLinkDocument("smelting", "plates", 2)],
            new GridRectangleDocument(0, 0, 6, 6),
            GapX: 0,
            GapY: 0,
            Together: 0,
            Seconds: 5,
            Floor: new BeltFloorDocument(SquaresPerPlotX: 10, SquaresPerPlotY: 10, RoadSquaresX: 4, RoadSquaresY: 4, Levels: 16));

        ArrangementProposalDocument proposal = ArrangementDocuments.Solve(problem);

        Assert.Equal(2, proposal.Belts?.Count);
        Assert.Empty(proposal.Unrouted ?? [new ArrangementLinkDocument("?", "?", 0)]);
        Assert.Equal(1, proposal.TallestStack);
    }

    [Fact]
    public void Without_a_layout_no_belts_are_routed()
    {
        var problem = new ArrangementProblemDocument(
            [new ArrangedFactoryDocument("smelting", new GridRectangleDocument(0, 0, 1, 1)), new ArrangedFactoryDocument("plates", new GridRectangleDocument(0, 0, 1, 1))],
            [],
            [new ArrangementLinkDocument("smelting", "plates", 1)],
            new GridRectangleDocument(0, 0, 1, 1),
            GapX: 0,
            GapY: 0,
            Together: 0,
            Seconds: 5,
            Floor: new BeltFloorDocument(SquaresPerPlotX: 10, SquaresPerPlotY: 10, RoadSquaresX: 4, RoadSquaresY: 4, Levels: 16));

        ArrangementProposalDocument proposal = ArrangementDocuments.Solve(problem);

        Assert.Equal((ArrangementQuality.NotFound, 0), (proposal.Quality, proposal.Places.Count));
        Assert.Null(proposal.Belts);
    }

    [Fact]
    public void Routed_belts_carry_their_level_on_each_run()
    {
        var problem = new ArrangementProblemDocument(
            [new ArrangedFactoryDocument("smelting", new GridRectangleDocument(0, 0, 1, 1)), new ArrangedFactoryDocument("plates", new GridRectangleDocument(5, 5, 1, 1))],
            [],
            [new ArrangementLinkDocument("smelting", "plates", 1)],
            new GridRectangleDocument(0, 0, 6, 6),
            GapX: 0,
            GapY: 0,
            Together: 0,
            Seconds: 5,
            Floor: new BeltFloorDocument(SquaresPerPlotX: 10, SquaresPerPlotY: 10, RoadSquaresX: 4, RoadSquaresY: 4, Levels: 16));

        RoutedBeltDocument belt = Assert.Single(ArrangementDocuments.Solve(problem).Belts!);

        Assert.Equal(belt.Corners.Count - 1, belt.Levels?.Count);
        Assert.Empty(belt.Lifts!);
    }

    [Fact]
    public void A_problem_from_before_the_belt_floor_gets_no_belts()
    {
        var problem = new ArrangementProblemDocument(
            [new ArrangedFactoryDocument("smelting", new GridRectangleDocument(0, 0, 1, 1)), new ArrangedFactoryDocument("plates", new GridRectangleDocument(5, 5, 1, 1))],
            [],
            [new ArrangementLinkDocument("smelting", "plates", 1)],
            new GridRectangleDocument(0, 0, 6, 6),
            GapX: 0,
            GapY: 0,
            Together: 0,
            Seconds: 5);

        Assert.Null(ArrangementDocuments.Solve(problem).Belts);
    }

    [Fact]
    public void Belts_from_nodes_and_stations_are_routed_from_their_spots()
    {
        var problem = new ArrangementProblemDocument(
            [new ArrangedFactoryDocument("smelting", new GridRectangleDocument(0, 0, 1, 1)), new ArrangedFactoryDocument("plates", new GridRectangleDocument(5, 5, 1, 1))],
            [new FixedPlaceDocument("miner", new GridRectangleDocument(5, 5, 1, 1), Blocks: false)],
            [new ArrangementLinkDocument("smelting", "plates", 1), new ArrangementLinkDocument("miner", "smelting", 1, WeightPercent: 50)],
            new GridRectangleDocument(0, 0, 6, 6),
            GapX: 0,
            GapY: 0,
            Together: 0,
            Seconds: 5,
            Floor: new BeltFloorDocument(10, 10, 4, 4, 16),
            Spots: [new SpotDocument("miner", new GridRectangleDocument(55, 55, 2, 2))]);

        ArrangementProposalDocument proposal = ArrangementDocuments.Solve(problem);

        Assert.Contains(proposal.Belts!, belt => belt.From == "miner" && belt.To == "smelting" && !belt.LeavesFloor);
    }
}
