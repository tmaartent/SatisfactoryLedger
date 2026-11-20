using System.Text.Json;

namespace SatisfactoryLedger.Mcp.Tests;

public class TraceToolsTests
{
    [Fact]
    public async Task Traces_a_shortage_back_to_where_it_starts_in_in_game_names()
    {
        await using var server = await LedgerMcpTestServer.StartAsync(IronWorld.Api());

        JsonElement trace = await server.CallAsync("trace_upstream", new() { ["building"] = "Constructor", ["recipe"] = "Iron Plate" });

        Assert.Equal(2, trace.GetProperty("traced").GetInt32());
        JsonElement start = trace.GetProperty("starts")[0];
        Assert.Equal("tooFewProducers", start.GetProperty("cause").GetString());
        Assert.Equal("Iron Ingot", start.GetProperty("item").GetString());
        Assert.Equal("Smelter", start.GetProperty("at")[0].GetProperty("building").GetString());
        Assert.Equal("Iron Ingot", start.GetProperty("at")[0].GetProperty("recipe").GetString());
        Assert.Equal(12.5, start.GetProperty("at")[0].GetProperty("positionMetres").GetProperty("x").GetDouble());
        JsonElement step = start.GetProperty("path")[0];
        Assert.Equal(("Constructor", 2, "Iron Ingot", 30.0), (step.GetProperty("building").GetString(), step.GetProperty("machines").GetInt32(), step.GetProperty("shortOf").GetString(), step.GetProperty("perMinute").GetDouble()));
    }

    [Fact]
    public async Task Traces_a_blockage_on_to_where_it_starts_naming_who_else_supplies_the_consumers()
    {
        await using var server = await LedgerMcpTestServer.StartAsync(IronWorld.Api());

        JsonElement start = (await server.CallAsync("trace_downstream", new() { ["building"] = "Constructor", ["recipe"] = "Iron Plate" })).GetProperty("starts")[0];

        Assert.Equal("suppliedByOthers", start.GetProperty("cause").GetString());
        JsonElement other = start.GetProperty("others")[0];
        Assert.Equal(("Smelter", 10.0), (other.GetProperty("building").GetString(), other.GetProperty("perMinute").GetDouble()));
        Assert.Equal("Iron Plate", start.GetProperty("path")[0].GetProperty("cannotLeave").GetString());
    }

    [Fact]
    public async Task Says_so_when_none_of_the_machines_asked_about_is_held_back()
    {
        await using var server = await LedgerMcpTestServer.StartAsync(IronWorld.Api());

        JsonElement trace = await server.CallAsync("trace_upstream", new() { ["building"] = "Smelter" });

        Assert.Contains("None of the 1", trace.GetProperty("note").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Names_the_recipes_of_the_building_when_the_one_asked_is_not_among_them()
    {
        await using var server = await LedgerMcpTestServer.StartAsync(IronWorld.Api());

        string error = await server.CallFailingAsync("trace_upstream", new() { ["building"] = "Smelter", ["recipe"] = "Iron Plate" });

        Assert.Contains("Iron Ingot", error, StringComparison.Ordinal);
    }
}
