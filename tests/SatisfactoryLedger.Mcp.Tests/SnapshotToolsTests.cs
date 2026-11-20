using System.Text.Json;

namespace SatisfactoryLedger.Mcp.Tests;

public class SnapshotToolsTests
{
    [Fact]
    public async Task Lists_the_snapshots_newest_first_with_their_world_and_whether_they_are_analysed()
    {
        await using var server = await LedgerMcpTestServer.StartAsync(IronWorld.Api());

        JsonElement snapshots = await server.CallAsync("list_snapshots");

        Assert.Equal([IronWorld.Newest, IronWorld.Older], snapshots.EnumerateArray().Select(s => s.GetProperty("id").GetString()));
        Assert.Equal("Iron Valley", snapshots[0].GetProperty("world").GetString());
        Assert.Equal("ready", snapshots[0].GetProperty("analysis").GetString());
    }

    [Fact]
    public async Task Sums_up_the_newest_snapshot_when_none_is_named()
    {
        await using var server = await LedgerMcpTestServer.StartAsync(IronWorld.Api());

        JsonElement summary = await server.CallAsync("get_findings_summary");

        Assert.Equal(IronWorld.Newest, summary.GetProperty("snapshotId").GetString());
        JsonElement machines = summary.GetProperty("machines");
        Assert.Equal(5, machines.GetProperty("total").GetInt32());
        Assert.Equal(2, machines.GetProperty("starved").GetInt32());
        Assert.Equal(1, summary.GetProperty("bottlenecks").GetInt32());
        Assert.Equal(1, summary.GetProperty("unusedSupply").GetInt32());
    }

    [Fact]
    public async Task Groups_the_machines_below_full_speed_by_building_recipe_and_cause_the_biggest_loss_first()
    {
        await using var server = await LedgerMcpTestServer.StartAsync(IronWorld.Api());

        JsonElement groups = (await server.CallAsync("get_findings_summary")).GetProperty("belowFullSpeed");

        // Two plate constructors at half speed lose a whole machine; the blocked one 0.2; the one without a recipe 1.
        JsonElement starved = groups[0];
        Assert.Equal("Constructor", starved.GetProperty("building").GetString());
        Assert.Equal("Iron Plate", starved.GetProperty("recipe").GetString());
        Assert.Equal("starved", starved.GetProperty("status").GetString());
        Assert.Equal(2, starved.GetProperty("machines").GetInt32());
        Assert.Equal(50, starved.GetProperty("averageUtilisationPercent").GetDouble());
        Assert.Equal(50, starved.GetProperty("shortOf").GetProperty("Iron Ingot").GetInt32());
        Assert.Equal(["starved", "noRecipe", "outputBlocked"], groups.EnumerateArray().Select(g => g.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task Says_a_snapshot_is_still_being_analysed_instead_of_giving_findings()
    {
        await using var server = await LedgerMcpTestServer.StartAsync(IronWorld.Analysing());

        string error = await server.CallFailingAsync("get_findings_summary");

        Assert.Contains("still being analysed", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Names_the_snapshots_there_are_when_asked_about_one_that_is_not()
    {
        await using var server = await LedgerMcpTestServer.StartAsync(IronWorld.Api());

        string error = await server.CallFailingAsync("get_findings_summary", new() { ["snapshotId"] = "99999999-9999-9999-9999-999999999999" });

        Assert.Contains("list_snapshots", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Lists_the_bottlenecks_with_what_they_carry_of_their_capacity()
    {
        await using var server = await LedgerMcpTestServer.StartAsync(IronWorld.Api());

        JsonElement findings = await server.CallAsync("list_findings", new() { ["kind"] = "bottlenecks" });

        Assert.Equal(1, findings.GetProperty("total").GetInt32());
        JsonElement belt = findings.GetProperty("findings")[0];
        Assert.Equal("Conveyor Belt Mk.3", belt.GetProperty("building").GetString());
        Assert.Equal(270, belt.GetProperty("carriesPerMinute").GetDouble());
        Assert.Equal(270, belt.GetProperty("capacityPerMinute").GetDouble());
        Assert.Equal(30, belt.GetProperty("offeredMorePerMinute").GetDouble());
    }

    [Fact]
    public async Task Lists_the_unused_supply_with_the_item_by_its_in_game_name()
    {
        await using var server = await LedgerMcpTestServer.StartAsync(IronWorld.Api());

        JsonElement supply = (await server.CallAsync("list_findings", new() { ["kind"] = "unusedSupply" })).GetProperty("findings")[0];

        Assert.Equal("Miner Mk.2", supply.GetProperty("building").GetString());
        Assert.Equal("Iron Ore", supply.GetProperty("item").GetString());
        Assert.Equal(60, supply.GetProperty("unusedPerMinute").GetDouble());
    }
}
