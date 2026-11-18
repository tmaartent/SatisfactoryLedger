using SatisfactoryLedger.World.Domain.Arrangement;

namespace SatisfactoryLedger.World.Domain.Tests.Arrangement;

/// <summary>
/// Extractors feeding a factory one item merge onto belts up to the top belt's capacity (decided 2026-11-18): not a belt
/// each. The nearest merge first.
/// </summary>
public class SupplyBeltsTests
{
    private static IReadOnlyList<IReadOnlyList<string>> Merged(double capacity, params Supply[] supplies) =>
        [.. SupplyBelts.Merge(supplies, capacity).Select(belt => (IReadOnlyList<string>)[.. belt.Select(supply => supply.Id).Order(StringComparer.Ordinal)])];

    [Fact]
    public void Limestone_from_three_miners_merges_onto_one_full_belt()
    {
        IReadOnlyList<IReadOnlyList<string>> belts = Merged(
            1200, new Supply("a", 0, 0, 300), new Supply("b", 10, 0, 300), new Supply("c", 20, 0, 600));

        Assert.Equal([["a", "b", "c"]], belts);
    }

    [Fact]
    public void What_a_belt_cannot_carry_goes_on_the_next()
    {
        IReadOnlyList<IReadOnlyList<string>> belts = Merged(
            1200, new Supply("a", 0, 0, 600), new Supply("b", 10, 0, 600), new Supply("c", 20, 0, 600));

        Assert.Equal(2, belts.Count);
        Assert.Equal(3, belts.Sum(belt => belt.Count));
    }

    [Fact]
    public void The_nearest_extractors_share_a_belt()
    {
        // a and c stand together; b far away.
        IReadOnlyList<IReadOnlyList<string>> belts = Merged(
            1200, new Supply("a", 0, 0, 600), new Supply("b", 5000, 0, 600), new Supply("c", 10, 0, 600));

        Assert.Contains(["a", "c"], belts);
        Assert.Contains(["b"], belts);
    }

    [Fact]
    public void What_an_item_needs_more_than_one_belt_for_fills_them_one_after_the_other()
    {
        Assert.Equal([1200, 300], SupplyBelts.Loads(1500, 1200));
        Assert.Equal([600], SupplyBelts.Loads(600, 1200));
        Assert.Equal([1200, 1200], SupplyBelts.Loads(2400, 1200));
    }

    [Fact]
    public void Water_extractors_fill_pipes_of_their_capacity()
    {
        IReadOnlyList<IReadOnlyList<string>> pipes = Merged(
            600, new Supply("a", 0, 0, 250), new Supply("b", 1, 0, 250), new Supply("c", 2, 0, 250), new Supply("d", 3, 0, 250));

        Assert.Equal([2, 2], pipes.Select(pipe => pipe.Count));
    }
}
