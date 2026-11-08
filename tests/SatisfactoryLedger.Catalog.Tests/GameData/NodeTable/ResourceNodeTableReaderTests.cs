using System.Text;

using SatisfactoryLedger.Catalog.Domain;
using SatisfactoryLedger.Catalog.GameData.NodeTable;

namespace SatisfactoryLedger.Catalog.Tests.GameData.NodeTable;

/// <summary>Reading the node table the map tool writes (ADR-0015) into resource nodes.</summary>
public class ResourceNodeTableReaderTests
{
    private static async Task<ResourceNodeTable> ReadFixtureAsync()
    {
        await using FileStream file = File.OpenRead("Fixtures/resource-nodes.json");
        return await ResourceNodeTableReader.ReadAsync(file, TestContext.Current.CancellationToken);
    }

    private static Task<ResourceNodeTable> ReadAsync(string json) =>
        ResourceNodeTableReader.ReadAsync(new MemoryStream(Encoding.UTF8.GetBytes(json)), TestContext.Current.CancellationToken);

    private static string Table(string node, int version = 1) =>
        $$"""{ "version": {{version}}, "gameVersion": "1.2", "nodes": [ {{node}} ] }""";

    private const string IronNode =
        """{ "name": "BP_ResourceNode35", "kind": "resourceNode", "resource": "Desc_OreIron_C", "purity": "normal", "position": { "x": 1, "y": 2, "z": 3 } }""";

    [Fact]
    public async Task The_table_from_the_game_has_every_resource_node_and_well_satellite()
    {
        ResourceNodeTable table = await ReadFixtureAsync();

        Assert.Equal("1.2", table.GameVersion);
        Assert.Equal(459, table.Nodes.Count(node => node.Kind == ResourceNodeKind.ResourceNode));
        Assert.Equal(118, table.Nodes.Count(node => node.Kind == ResourceNodeKind.WellSatellite));
    }

    [Fact]
    public async Task The_node_the_test_world_miner_stands_on_is_normal_iron_ore()
    {
        ResourceNode node = Assert.Single((await ReadFixtureAsync()).Nodes, node => node.Id.Id == "BP_ResourceNode35");

        Assert.Equal(ItemId.Of("Desc_OreIron_C"), node.Resource);
        Assert.Equal(Purity.Normal, node.Purity);
        Assert.Equal(256611.65625, node.Position.X);
    }

    [Theory]
    [InlineData("impure", "Impure")]
    [InlineData("normal", "Normal")]
    [InlineData("pure", "Pure")]
    public async Task A_purity_is_read(string written, string expected)
    {
        ResourceNodeTable table = await ReadAsync(Table(IronNode.Replace("\"normal\"", $"\"{written}\"", StringComparison.Ordinal)));

        Assert.Equal(Enum.Parse<Purity>(expected), Assert.Single(table.Nodes).Purity);
    }

    [Fact]
    public async Task A_table_in_another_format_version_is_refused()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(Table(IronNode, version: 2)));
    }

    [Fact]
    public async Task An_unknown_purity_is_refused()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(Table(IronNode.Replace("\"normal\"", "\"legendary\"", StringComparison.Ordinal))));
    }

    [Fact]
    public async Task A_node_without_a_position_is_refused()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            ReadAsync(Table("""{ "name": "BP_ResourceNode35", "kind": "resourceNode", "resource": "Desc_OreIron_C", "purity": "normal" }""")));
    }

    [Fact]
    public async Task A_file_that_is_not_a_node_table_is_refused()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => ReadAsync("this is not json"));
    }
}
