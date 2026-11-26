using System.Text.Json;

using ModelContextProtocol.Client;

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
        Assert.Equal("too few producers", start.GetProperty("causeTerm").GetString());
        Assert.Equal("Iron Ingot", start.GetProperty("item").GetString());
        Assert.Equal("Smelter", start.GetProperty("at")[0].GetProperty("building").GetString());
        Assert.Equal("Iron Ingot", start.GetProperty("at")[0].GetProperty("recipe").GetString());
        Assert.Equal(12.5, start.GetProperty("at")[0].GetProperty("positionMetres").GetProperty("x").GetDouble());
        JsonElement step = start.GetProperty("path")[0];
        Assert.Equal(("Constructor", 2, "Iron Ingot", 30.0), (step.GetProperty("building").GetString(), step.GetProperty("machines").GetInt32(), step.GetProperty("shortOf").GetString(), step.GetProperty("perMinute").GetDouble()));
    }

    [Theory]
    [InlineData("trace_upstream", "taken by others", "takenByOthers")]
    [InlineData("trace_downstream", "supplied by others", "suppliedByOthers")]
    [InlineData("trace_downstream", "no drone refuels here", "noDroneRefuels")]
    public async Task Describes_each_cause_by_the_term_its_answer_gives_it_not_by_its_code(string tool, string term, string code)
    {
        await using var server = await LedgerMcpTestServer.StartAsync(IronWorld.Api());

        IList<McpClientTool> tools = await server.Client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        string description = tools.Single(listed => listed.Name == tool).Description;
        Assert.Contains($"{term} (", description, StringComparison.Ordinal);
        Assert.DoesNotContain(code, description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Names_the_machines_of_each_step_by_their_ids_so_they_can_be_found_on_the_map()
    {
        await using var server = await LedgerMcpTestServer.StartAsync(IronWorld.Api());

        JsonElement trace = await server.CallAsync("trace_upstream", new() { ["building"] = "Constructor", ["recipe"] = "Iron Plate" });

        JsonElement step = trace.GetProperty("starts")[0].GetProperty("path")[0];
        Assert.Equal(["m-1", "m-2"], step.GetProperty("ids").EnumerateArray().Select(id => id.GetString()));
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
    public async Task Says_what_a_bottleneck_needs_to_carry_all_that_is_offered_to_it_by_the_tier_s_name()
    {
        await using var server = await LedgerMcpTestServer.StartAsync(IronWorld.Api());

        JsonElement start = (await server.CallAsync("trace_downstream", new() { ["building"] = "Smelter" })).GetProperty("starts")[0];

        Assert.Equal("bottleneck", start.GetProperty("causeTerm").GetString());
        JsonElement more = start.GetProperty("moreCapacity");
        Assert.Equal(("Conveyor Belt Mk.4", 1, 300.0), (more.GetProperty("tier").GetString(), more.GetProperty("lines").GetInt32(), more.GetProperty("carriesPerMinute").GetDouble()));
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
