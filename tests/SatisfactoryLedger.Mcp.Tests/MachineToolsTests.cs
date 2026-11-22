using System.Text.Json;

namespace SatisfactoryLedger.Mcp.Tests;

public class MachineToolsTests
{
    [Fact]
    public async Task Finds_the_machines_making_an_item_by_its_in_game_name_with_what_holds_each_back()
    {
        await using var server = await LedgerMcpTestServer.StartAsync(IronWorld.Api());

        JsonElement found = await server.CallAsync("find_machines", new() { ["makes"] = "iron plate" });

        Assert.Equal(3, found.GetProperty("total").GetInt32());
        JsonElement first = found.GetProperty("machines")[0];
        Assert.Equal("m-1", first.GetProperty("id").GetString());
        Assert.Equal("starved", first.GetProperty("status").GetString());
        Assert.Equal(50, first.GetProperty("utilisationPercent").GetDouble());
        Assert.Equal(50, first.GetProperty("shortOf").GetProperty("Iron Ingot").GetInt32());
    }

    [Fact]
    public async Task Finds_the_machines_of_one_status_in_metres_where_they_stand()
    {
        await using var server = await LedgerMcpTestServer.StartAsync(IronWorld.Api());

        JsonElement found = await server.CallAsync("find_machines", new() { ["status"] = "outputBlocked" });

        JsonElement blocked = Assert.Single(found.GetProperty("machines").EnumerateArray());
        Assert.Equal(80, blocked.GetProperty("blockedOn").GetProperty("Iron Plate").GetInt32());
        Assert.Equal(12.5, blocked.GetProperty("positionMetres").GetProperty("x").GetDouble());
    }

    [Fact]
    public async Task Suggests_items_with_a_like_name_when_none_is_called_that()
    {
        await using var server = await LedgerMcpTestServer.StartAsync(IronWorld.Api());

        string error = await server.CallFailingAsync("find_machines", new() { ["makes"] = "iron" });

        Assert.Contains("Iron Plate", error, StringComparison.Ordinal);
        Assert.Contains("Iron Ingot", error, StringComparison.Ordinal);
    }
}
