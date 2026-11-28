using System.Text.Json;

namespace SatisfactoryLedger.Mcp.Tests;

public class FactoryToolsTests
{
    [Fact]
    public async Task Lists_the_factories_of_a_snapshot_with_their_machine_count()
    {
        await using var server = await LedgerMcpTestServer.StartAsync(IronWorld.Api());

        JsonElement factory = (await server.CallAsync("list_factories"))[0];

        Assert.Equal("Plates", factory.GetProperty("name").GetString());
        Assert.Equal(3, factory.GetProperty("machines").GetInt32());
    }

    [Fact]
    public async Task Lists_the_factories_by_name_though_the_ledger_gives_them_oldest_first()
    {
        static object Factory(string id, string name) => new
        {
            factoryId = id, name, traceable = true, logistics = 0,
            machines = Array.Empty<object>(), outputs = Array.Empty<object>(), inputs = Array.Empty<object>(), missing = Array.Empty<string>(),
        };
        FakeLedgerApi api = IronWorld.Api().Answer(
            $"/api/v1/world/snapshots/{IronWorld.Newest}/factories",
            new[] { Factory("44444444-4444-4444-4444-444444444444", "Wire"), Factory("55555555-5555-5555-5555-555555555555", "Bolts") });
        await using var server = await LedgerMcpTestServer.StartAsync(api);

        JsonElement factories = await server.CallAsync("list_factories");

        Assert.Equal(["Bolts", "Wire"], factories.EnumerateArray().Select(factory => factory.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task Gives_a_factorys_findings_by_its_name_with_what_comes_in_and_goes_out_by_in_game_name()
    {
        await using var server = await LedgerMcpTestServer.StartAsync(IronWorld.Api());

        JsonElement findings = await server.CallAsync("get_factory_findings", new() { ["factory"] = "plates" });

        Assert.Equal(IronWorld.PlateFactory, findings.GetProperty("factoryId").GetString());
        JsonElement input = findings.GetProperty("input")[0];
        Assert.Equal("Iron Ingot", input.GetProperty("item").GetString());
        Assert.Equal(30, input.GetProperty("nowPerMinute").GetDouble());
        Assert.Equal(60, input.GetProperty("shortPerMinute").GetDouble());
        Assert.Equal("Iron Plate", findings.GetProperty("output")[0].GetProperty("item").GetString());
        Assert.Equal(2, findings.GetProperty("belowFullSpeed")[0].GetProperty("machines").GetInt32());
    }

    [Fact]
    public async Task Names_the_factories_there_are_when_asked_about_one_that_is_not()
    {
        await using var server = await LedgerMcpTestServer.StartAsync(IronWorld.Api());

        string error = await server.CallFailingAsync("get_factory_findings", new() { ["factory"] = "Screws" });

        Assert.Contains("Plates", error, StringComparison.Ordinal);
    }
}
