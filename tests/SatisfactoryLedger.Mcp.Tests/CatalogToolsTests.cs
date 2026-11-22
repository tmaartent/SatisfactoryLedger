using System.Text.Json;

namespace SatisfactoryLedger.Mcp.Tests;

public class CatalogToolsTests
{
    [Fact]
    public async Task Gives_the_recipes_for_an_item_by_its_in_game_name_with_each_ingredient_per_minute()
    {
        await using var server = await LedgerMcpTestServer.StartAsync(IronWorld.Api());

        JsonElement recipes = await server.CallAsync("get_recipes", new() { ["item"] = "Iron Plate" });

        Assert.Equal(["Iron Plate", "Alternate: Coated Iron Plate"], recipes.EnumerateArray().Select(r => r.GetProperty("name").GetString()));
        JsonElement plate = recipes[0];
        Assert.Equal("Constructor", plate.GetProperty("building").GetString());
        Assert.Equal("Iron Ingot", plate.GetProperty("inputs")[0].GetProperty("item").GetString());
        Assert.Equal(30, plate.GetProperty("inputs")[0].GetProperty("perMinute").GetDouble());
        Assert.Equal(20, plate.GetProperty("outputs")[0].GetProperty("perMinute").GetDouble());
    }

    [Fact]
    public async Task Leaves_out_the_alternate_recipes_when_asked()
    {
        await using var server = await LedgerMcpTestServer.StartAsync(IronWorld.Api());

        JsonElement recipes = await server.CallAsync("get_recipes", new() { ["item"] = "Iron Plate", ["includeAlternates"] = false });

        Assert.Equal(["Iron Plate"], recipes.EnumerateArray().Select(r => r.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task Finds_the_items_whose_name_contains_the_words_asked()
    {
        await using var server = await LedgerMcpTestServer.StartAsync(IronWorld.Api());

        JsonElement items = await server.CallAsync("find_items", new() { ["name"] = "iron" });

        Assert.Equal(["Iron Ingot", "Iron Ore", "Iron Plate", "Iron Rod"], items.EnumerateArray().Select(i => i.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task Gives_the_belt_and_pipe_tiers_with_their_capacity_per_minute()
    {
        await using var server = await LedgerMcpTestServer.StartAsync(IronWorld.Api());

        JsonElement tiers = await server.CallAsync("get_transport_tiers");

        Assert.Equal("Conveyor Belt Mk.3", tiers[0].GetProperty("name").GetString());
        Assert.Equal(270, tiers[0].GetProperty("capacityPerMinute").GetInt32());
        Assert.Equal("pipe", tiers[1].GetProperty("form").GetString());
    }
}
