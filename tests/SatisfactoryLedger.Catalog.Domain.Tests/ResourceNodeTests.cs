namespace SatisfactoryLedger.Catalog.Domain.Tests;

public class ResourceNodeTests
{
    private static readonly MapPosition Somewhere = new(256611.66, -197978.56, 3561.47);

    private static ResourceNode IronNode(Purity purity = Purity.Normal) =>
        ResourceNode.Create(ResourceNodeId.Of("BP_ResourceNode35"), ResourceNodeKind.ResourceNode, ItemId.Of("Desc_OreIron_C"), purity, Somewhere);

    [Fact]
    public void A_resource_node_id_cannot_be_blank()
    {
        Assert.Throws<ArgumentException>(() => ResourceNodeId.Of(" "));
    }

    [Fact]
    public void A_resource_node_has_its_resource_purity_and_position()
    {
        ResourceNode node = IronNode(Purity.Pure);

        Assert.Equal(ItemId.Of("Desc_OreIron_C"), node.Resource);
        Assert.Equal(Purity.Pure, node.Purity);
        Assert.Equal(Somewhere, node.Position);
        Assert.Equal(ResourceNodeKind.ResourceNode, node.Kind);
    }

    [Fact]
    public void Resource_nodes_with_the_same_name_are_equal()
    {
        Assert.Equal(IronNode(Purity.Pure), IronNode(Purity.Impure));
        Assert.Equal(IronNode(Purity.Pure).GetHashCode(), IronNode(Purity.Impure).GetHashCode());
    }

    [Fact]
    public void A_node_without_a_position_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ResourceNode.Create(ResourceNodeId.Of("BP_ResourceNode35"), ResourceNodeKind.ResourceNode, ItemId.Of("Desc_OreIron_C"), Purity.Normal, null!));
    }

    [Fact]
    public void Updating_a_node_from_the_game_takes_the_game_data()
    {
        ResourceNode stored = IronNode(Purity.Impure);
        stored.MarkNoLongerInGame();

        stored.UpdateFromGame(IronNode(Purity.Pure));

        Assert.Equal(Purity.Pure, stored.Purity);
        Assert.True(stored.IsInGame);
    }

    [Fact]
    public void A_node_cannot_be_updated_from_a_different_node()
    {
        ResourceNode other = ResourceNode.Create(ResourceNodeId.Of("BP_ResourceNode36"), ResourceNodeKind.ResourceNode, ItemId.Of("Desc_OreIron_C"), Purity.Normal, Somewhere);

        Assert.Throws<ArgumentException>(() => IronNode().UpdateFromGame(other));
    }

    [Fact]
    public void A_node_marked_no_longer_in_game_is_not_in_the_game()
    {
        ResourceNode node = IronNode();

        node.MarkNoLongerInGame();

        Assert.False(node.IsInGame);
    }
}
