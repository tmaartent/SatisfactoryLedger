using SatisfactoryLedger.World.Contracts.Arrangement;
using SatisfactoryLedger.World.Domain.Arrangement;

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
        Assert.Equal(1, BeltFloor.LevelsUsed(proposal.Belts!.Select(belt => belt.Levels!)));
    }

    [Fact]
    public void A_layout_whose_belts_all_route_is_placed_once()
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

        Assert.Equal(1, proposal.Placements);
    }

    [Fact]
    public void When_belts_are_left_unrouted_it_places_again_up_to_three_times_and_keeps_the_fewest_left()
    {
        // Step 101 (asked 2026-11-25): a strip one square wide, with one level, holds one belt of the two the link needs,
        // wherever the factories stand. Each new placement differs from the ones before.
        var problem = new ArrangementProblemDocument(
            [new ArrangedFactoryDocument("smelting", new GridRectangleDocument(0, 0, 1, 1)), new ArrangedFactoryDocument("plates", new GridRectangleDocument(5, 0, 1, 1))],
            [],
            [new ArrangementLinkDocument("smelting", "plates", 2)],
            new GridRectangleDocument(0, 0, 6, 1),
            GapX: 0,
            GapY: 0,
            Together: 0,
            Seconds: 5,
            Floor: new BeltFloorDocument(SquaresPerPlotX: 1, SquaresPerPlotY: 1, RoadSquaresX: 0, RoadSquaresY: 0, Levels: 1));

        ArrangementProposalDocument proposal = ArrangementDocuments.Solve(problem);

        Assert.Equal(3, proposal.Placements);
        Assert.Equal(1, proposal.Unrouted!.Sum(link => link.Belts + link.Pipes));
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
    public void Each_routed_belt_carries_its_links_load()
    {
        var problem = new ArrangementProblemDocument(
            [new ArrangedFactoryDocument("smelting", new GridRectangleDocument(0, 0, 1, 1)), new ArrangedFactoryDocument("plates", new GridRectangleDocument(5, 5, 1, 1))],
            [],
            [new ArrangementLinkDocument("smelting", "plates", 2, Loads: [new BeltLoadDocument("Desc_IronIngot_C", 1200, Pipe: false), new BeltLoadDocument("Desc_IronIngot_C", 300, Pipe: false)])],
            new GridRectangleDocument(0, 0, 6, 6),
            GapX: 0,
            GapY: 0,
            Together: 0,
            Seconds: 5,
            Floor: new BeltFloorDocument(SquaresPerPlotX: 10, SquaresPerPlotY: 10, RoadSquaresX: 4, RoadSquaresY: 4, Levels: 16));

        IReadOnlyList<RoutedBeltDocument> belts = ArrangementDocuments.Solve(problem).Belts!;

        Assert.Equal([1200.0, 300.0], belts.OrderBy(belt => belt.Index).Select(belt => belt.Load?.PerMinute));
    }

    [Fact]
    public void With_supply_lines_a_line_runs_past_the_factories_instead_of_a_belt_to_each()
    {
        // Miners of 900 by the first plot; three factories taking 300 each. Placing still counts a belt to each.
        var problem = new ArrangementProblemDocument(
            [
                new ArrangedFactoryDocument("first", new GridRectangleDocument(0, 0, 1, 1)),
                new ArrangedFactoryDocument("second", new GridRectangleDocument(1, 0, 1, 1)),
                new ArrangedFactoryDocument("third", new GridRectangleDocument(2, 0, 1, 1)),
            ],
            [new FixedPlaceDocument("miners", new GridRectangleDocument(0, 0, 1, 1), Blocks: false)],
            [new ArrangementLinkDocument("miners", "first", 1), new ArrangementLinkDocument("miners", "second", 1), new ArrangementLinkDocument("miners", "third", 1)],
            new GridRectangleDocument(0, 0, 3, 1),
            GapX: 0,
            GapY: 0,
            Together: 0,
            Seconds: 5,
            Floor: new BeltFloorDocument(SquaresPerPlotX: 10, SquaresPerPlotY: 10, RoadSquaresX: 4, RoadSquaresY: 4, Levels: 16),
            Spots: [new SpotDocument("miners", new GridRectangleDocument(0, 3, 1, 1))],
            Supplies:
            [
                new ItemSupplyDocument(
                    "Desc_OreIron_C",
                    Pipe: false,
                    Capacity: 1200,
                    [new SupplyEndDocument("miners", 900)],
                    [new SupplyEndDocument("first", 300), new SupplyEndDocument("second", 300), new SupplyEndDocument("third", 300)]),
            ]);

        IReadOnlyList<RoutedBeltDocument> belts = ArrangementDocuments.Solve(problem).Belts!;

        Assert.Equal(
            [("Desc_OreIron_C", 300.0), ("Desc_OreIron_C", 600.0), ("Desc_OreIron_C", 900.0)],
            belts.Select(belt => (belt.Load!.Item, belt.Load.PerMinute)).Order());
        Assert.Single(belts, belt => belt.From == "miners");
    }

    [Fact]
    public void An_injection_is_routed_as_a_belt_to_the_factory_where_its_priority_merger_stands()
    {
        var problem = new ArrangementProblemDocument(
            [new ArrangedFactoryDocument("smelters", new GridRectangleDocument(1, 0, 1, 1))],
            [],
            [],
            new GridRectangleDocument(0, 0, 3, 1),
            GapX: 0,
            GapY: 0,
            Together: 0,
            Seconds: 5,
            Floor: new BeltFloorDocument(SquaresPerPlotX: 10, SquaresPerPlotY: 10, RoadSquaresX: 4, RoadSquaresY: 4, Levels: 16),
            Spots: [new SpotDocument("miners", new GridRectangleDocument(0, 3, 1, 1)), new SpotDocument("more", new GridRectangleDocument(40, 3, 1, 1))],
            Supplies:
            [
                new ItemSupplyDocument(
                    "Desc_OreIron_C",
                    Pipe: false,
                    Capacity: 1200,
                    [new SupplyEndDocument("miners", 600), new SupplyEndDocument("more", 300)],
                    [new SupplyEndDocument("smelters", 900)]),
            ]);

        IReadOnlyList<RoutedBeltDocument> belts = ArrangementDocuments.Solve(problem).Belts!;

        RoutedBeltDocument injection = Assert.Single(belts, belt => belt.Load?.Injection == true);
        Assert.Equal(("more", "smelters", 300.0), (injection.From, injection.To, injection.Load!.PerMinute));
    }

    [Fact]
    public void A_top_up_merges_onto_its_own_items_line_on_the_floor()
    {
        // One plot of 20 squares, the smelters on it; iron ore from the east, limestone from the north-east and more
        // limestone from the north-west. The legs of each item count from 0, so limestone's top-up must find its line
        // among all the legs, not ore's.
        var problem = new ArrangementProblemDocument(
            [new ArrangedFactoryDocument("smelters", new GridRectangleDocument(0, 0, 1, 1))],
            [],
            [],
            new GridRectangleDocument(0, 0, 1, 1),
            GapX: 0,
            GapY: 0,
            Together: 0,
            Seconds: 5,
            Floor: new BeltFloorDocument(SquaresPerPlotX: 20, SquaresPerPlotY: 20, RoadSquaresX: 14, RoadSquaresY: 14, Levels: 16),
            Spots:
            [
                new SpotDocument("ore", new GridRectangleDocument(19, 2, 1, 1)),
                new SpotDocument("limestone", new GridRectangleDocument(19, 19, 1, 1)),
                new SpotDocument("more", new GridRectangleDocument(2, 19, 1, 1)),
            ],
            Supplies:
            [
                new ItemSupplyDocument("Desc_OreIron_C", Pipe: false, Capacity: 1200, [new SupplyEndDocument("ore", 300)], [new SupplyEndDocument("smelters", 300)]),
                new ItemSupplyDocument(
                    "Desc_Stone_C",
                    Pipe: false,
                    Capacity: 1200,
                    [new SupplyEndDocument("limestone", 600), new SupplyEndDocument("more", 300)],
                    [new SupplyEndDocument("smelters", 900)]),
            ]);

        IReadOnlyList<RoutedBeltDocument> belts = ArrangementDocuments.Solve(problem).Belts!;

        RoutedBeltDocument topUp = Assert.Single(belts, belt => belt.From == "more");
        RoutedBeltDocument line = Assert.Single(belts, belt => belt.From == "limestone");
        Assert.True(topUp.EndsOnBelt);
        Assert.Contains(topUp.Corners[^1], Squares(line));
    }

    /// <summary>The squares a routed belt passes, corner to corner.</summary>
    private static List<FloorSquareDocument> Squares(RoutedBeltDocument belt)
    {
        List<FloorSquareDocument> squares = [belt.Corners[0]];
        foreach ((FloorSquareDocument a, FloorSquareDocument b) in belt.Corners.Zip(belt.Corners.Skip(1)))
        {
            for (int step = 1; step <= Math.Abs(b.X - a.X) + Math.Abs(b.Y - a.Y); step++)
            {
                squares.Add(new FloorSquareDocument(a.X + (Math.Sign(b.X - a.X) * step), a.Y + (Math.Sign(b.Y - a.Y) * step)));
            }
        }

        return squares;
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
