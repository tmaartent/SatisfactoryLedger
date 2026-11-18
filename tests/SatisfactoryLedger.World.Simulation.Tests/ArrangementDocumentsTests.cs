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
}
