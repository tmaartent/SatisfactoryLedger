namespace SatisfactoryLedger.World.Domain.Tests;

/// <summary>Which belts the tool suggests as a factory's output belt (BR-W12).</summary>
public class CandidateOutputBeltsTests
{
    private static IReadOnlyList<OutputBeltCandidate> Candidates(TestWorld world) => CandidateOutputBelts.In(world.Build());

    private static OutputBeltCandidate Single(TestWorld world, string belt) =>
        Assert.Single(Candidates(world), candidate => candidate.Belt == BuildableId.Of(belt));

    /// <summary>constructor → plates → container.</summary>
    private static TestWorld PlatesIntoContainer() => new TestWorld()
        .Machine("constructor", Items.IronIngot, Items.IronPlate).Belt("plates").Container("container")
        .Connect("constructor", "plates", "container");

    [Fact]
    public void A_belt_from_a_machine_into_a_storage_container_is_a_candidate()
    {
        OutputBeltCandidate candidate = Single(PlatesIntoContainer(), "plates");

        Assert.Equal([BuildableId.Of("container")], candidate.Into);
    }

    [Fact]
    public void A_candidate_carries_what_the_machines_feeding_it_produce()
    {
        Assert.Equal([Items.IronPlate], Single(PlatesIntoContainer(), "plates").Items);
    }

    [Fact]
    public void A_belt_into_a_sink_is_a_candidate()
    {
        var world = new TestWorld()
            .Machine("constructor", Items.IronIngot, Items.IronPlate).Belt("plates").Sink("sink")
            .Connect("constructor", "plates", "sink");

        Assert.Equal([BuildableId.Of("sink")], Single(world, "plates").Into);
    }

    [Fact]
    public void A_belt_that_ends_nowhere_is_a_candidate()
    {
        var world = new TestWorld()
            .Machine("constructor", Items.IronIngot, Items.IronRod).Belt("rods")
            .Connect("constructor", "rods");

        Assert.Empty(Single(world, "rods").Into);
    }

    [Fact]
    public void Only_the_last_belt_before_the_container_is_a_candidate()
    {
        var world = new TestWorld()
            .Machine("constructor", Items.IronIngot, Items.IronPlate).Belt("first").Lift("lift").Belt("last").Container("container")
            .Connect("constructor", "first", "lift", "last", "container");

        Assert.Equal([BuildableId.Of("last")], Candidates(world).Select(candidate => candidate.Belt));
    }

    [Fact]
    public void A_belt_that_feeds_a_machine_is_not_a_candidate()
    {
        var world = new TestWorld()
            .Machine("smelter", Items.IronOre, Items.IronIngot).Belt("ingots").Machine("constructor", Items.IronIngot, Items.IronPlate)
            .Connect("smelter", "ingots", "constructor");

        Assert.DoesNotContain(Candidates(world), candidate => candidate.Belt == BuildableId.Of("ingots"));
    }

    [Fact]
    public void A_belt_fed_only_by_a_provider_is_not_a_candidate()
    {
        var world = new TestWorld()
            .Miner("miner").Belt("ore").Container("container")
            .Connect("miner", "ore", "container");

        Assert.Empty(Candidates(world));
    }

    [Fact]
    public void A_belt_carries_the_products_of_every_machine_merged_into_it()
    {
        var world = new TestWorld()
            .Machine("plates", Items.IronIngot, Items.IronPlate).Machine("rods", Items.IronIngot, Items.IronRod)
            .Merger("merge").Belt("mixed").Container("container")
            .Connect("plates", "merge", "mixed", "container")
            .Connect("rods", "merge");

        Assert.Equal([Items.IronPlate, Items.IronRod], Single(world, "mixed").Items.OrderBy(item => item.Id, StringComparer.Ordinal));
    }

    [Fact]
    public void Machines_behind_another_machine_do_not_add_their_items()
    {
        var world = new TestWorld()
            .Machine("smelter", Items.IronOre, Items.IronIngot).Belt("ingots")
            .Machine("constructor", Items.IronIngot, Items.IronPlate).Belt("plates").Container("container")
            .Connect("smelter", "ingots", "constructor", "plates", "container");

        Assert.Equal([Items.IronPlate], Single(world, "plates").Items);
    }
}
