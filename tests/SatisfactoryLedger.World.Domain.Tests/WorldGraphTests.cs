namespace SatisfactoryLedger.World.Domain.Tests;

public class WorldGraphTests
{
    [Fact]
    public void A_buildable_id_cannot_be_blank()
    {
        Assert.Throws<ArgumentException>(() => BuildableId.Of(" "));
    }

    [Fact]
    public void An_item_id_cannot_be_blank()
    {
        Assert.Throws<ArgumentException>(() => ItemId.Of(""));
    }

    [Fact]
    public void A_buildable_is_in_the_world_only_once()
    {
        var world = new TestWorld().Belt("belt").Belt("belt");

        Assert.Throws<ArgumentException>(world.Build);
    }

    [Fact]
    public void A_connection_to_a_buildable_that_is_not_in_the_world_is_rejected()
    {
        var world = new TestWorld().Belt("belt").Connect("belt", "missing");

        Assert.Throws<ArgumentException>(world.Build);
    }

    [Fact]
    public void Upstream_of_a_buildable_is_what_feeds_it()
    {
        var world = new TestWorld()
            .Miner("miner").Belt("belt").Machine("smelter", Items.IronOre, Items.IronIngot)
            .Connect("miner", "belt", "smelter")
            .Build();

        Assert.Equal(Ids.Of("belt"), world.Upstream(BuildableId.Of("smelter")).Select(b => b.Id));
    }

    [Fact]
    public void Downstream_of_a_buildable_is_what_it_feeds()
    {
        var world = new TestWorld()
            .Belt("belt").Splitter("splitter")
            .Machine("left", Items.IronIngot, Items.IronPlate).Machine("right", Items.IronIngot, Items.IronRod)
            .Connect("belt", "splitter", "left")
            .Connect("splitter", "right")
            .Build();

        Assert.Equal(Ids.Of("left", "right"), world.Downstream(BuildableId.Of("splitter")).Select(b => b.Id));
    }
}
