namespace SatisfactoryLedger.MapExtractor.Tests;

/// <summary>From what the game's map says about a node to an entry of the node table (ADR-0015).</summary>
public class ResourceNodeTableTests
{
    private const string IronOre = "BlueprintGeneratedClass'/Game/FactoryGame/Resource/RawResources/OreIron/Desc_OreIron.Desc_OreIron_C'";

    private static MapNode Node(
        string name = "BP_ResourceNode35", string? resource = IronOre, string? purity = null, NodeKind kind = NodeKind.ResourceNode) =>
        new(name, kind, resource, purity, new NodePosition(256611.66, -197978.56, 3561.47));

    private static ResourceNodeEntry Single(MapNode node) => Assert.Single(ResourceNodeTable.Build("1.2", [node]).Nodes);

    [Fact]
    public void A_node_keeps_the_name_the_game_and_the_save_use()
    {
        Assert.Equal("BP_ResourceNode35", Single(Node()).Name);
    }

    [Fact]
    public void The_resource_is_the_class_name_of_the_item()
    {
        Assert.Equal("Desc_OreIron_C", Single(Node()).Resource);
    }

    // Unreal stores only values that differ from the default. 119 nodes store impure and 141 pure, so the default is normal.
    [Fact]
    public void A_node_without_a_stored_purity_is_normal()
    {
        Assert.Equal(Purity.Normal, Single(Node(purity: null)).Purity);
    }

    [Theory]
    [InlineData("RP_Inpure", "Impure")] // The game's own spelling.
    [InlineData("RP_Normal", "Normal")]
    [InlineData("RP_Pure", "Pure")]
    [InlineData("EResourcePurity::RP_Pure", "Pure")]
    public void A_stored_purity_is_translated(string stored, string expected)
    {
        Assert.Equal(Enum.Parse<Purity>(expected), Single(Node(purity: stored)).Purity);
    }

    [Fact]
    public void A_resource_well_satellite_is_a_node_of_its_own_kind()
    {
        const string nitrogen = "BlueprintGeneratedClass'/Game/FactoryGame/Resource/RawResources/NitrogenGas/Desc_NitrogenGas.Desc_NitrogenGas_C'";

        ResourceNodeEntry satellite = Single(Node("BP_FrackingSatellite10", nitrogen, "RP_Pure", NodeKind.WellSatellite));

        Assert.Equal(NodeKind.WellSatellite, satellite.Kind);
        Assert.Equal("Desc_NitrogenGas_C", satellite.Resource);
    }

    [Fact]
    public void An_unknown_purity_is_refused()
    {
        Assert.Throws<InvalidDataException>(() => ResourceNodeTable.Build("1.2", [Node(purity: "RP_Legendary")]));
    }

    [Fact]
    public void A_node_without_a_resource_is_refused()
    {
        Assert.Throws<InvalidDataException>(() => ResourceNodeTable.Build("1.2", [Node(resource: null)]));
    }

    [Fact]
    public void Two_nodes_with_the_same_name_are_refused()
    {
        Assert.Throws<InvalidDataException>(() => ResourceNodeTable.Build("1.2", [Node(), Node()]));
    }

    [Fact]
    public void The_table_lists_nodes_by_name_and_carries_the_game_version()
    {
        ResourceNodeTable table = ResourceNodeTable.Build("1.2", [Node("BP_ResourceNode9"), Node("BP_ResourceNode10")]);

        Assert.Equal("1.2", table.GameVersion);
        Assert.Equal(["BP_ResourceNode10", "BP_ResourceNode9"], table.Nodes.Select(node => node.Name));
    }

    [Fact]
    public void The_table_is_written_as_json_with_its_format_version()
    {
        string json = ResourceNodeTable.Build("1.2", [Node()]).ToJson();

        Assert.Contains("\"version\": 1", json);
        Assert.Contains("\"name\": \"BP_ResourceNode35\"", json);
        Assert.Contains("\"resource\": \"Desc_OreIron_C\"", json);
        Assert.Contains("\"purity\": \"normal\"", json);
        Assert.Contains("\"kind\": \"resourceNode\"", json);
    }
}
