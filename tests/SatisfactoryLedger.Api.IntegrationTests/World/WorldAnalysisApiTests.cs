using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

using SatisfactoryLedger.Api.IntegrationTests.Catalog;

namespace SatisfactoryLedger.Api.IntegrationTests.World;

/// <summary>The findings of a snapshot over the API (BR-A9, BR-A11 to A13), on a real PostgreSQL.</summary>
public sealed class WorldAnalysisApiTests(LedgerApiFactory api) : IAsyncLifetime
{
    private readonly HttpClient _client = api.CreateClient();

    public async ValueTask InitializeAsync() => await api.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<JsonNode> AnalyseTestWorldAsync()
    {
        await _client.ImportAsync(CatalogClient.GameFile);
        await _client.ImportResourceNodesAsync();
        Snapshot snapshot = await _client.ImportSaveAsync(WorldClient.TestWorld);
        await _client.WaitForAnalysisAsync(snapshot.Id);

        HttpResponseMessage response = await _client.GetAsync($"/api/v1/world/snapshots/{snapshot.Id}/analysis", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonNode>(TestContext.Current.CancellationToken))!;
    }

    [Fact]
    public async Task The_analysis_of_a_snapshot_counts_its_machines_by_how_they_run()
    {
        JsonNode analysis = await AnalyseTestWorldAsync();

        Assert.True(analysis["settled"]!.GetValue<bool>());
        Assert.Equal(
            """{"machines":5,"running":4,"starved":1,"outputBlocked":0,"noRecipe":0}""",
            analysis["summary"]!.ToJsonString());
    }

    [Fact]
    public async Task A_machine_below_100_percent_comes_with_its_cause_its_recipe_and_where_it_stands()
    {
        JsonNode analysis = await AnalyseTestWorldAsync();

        // The rod constructor gets no ingots. Smelter A, output blocked here before 2026-11-10, has room for its next
        // batch in the save (98 of 100 waiting), so it runs (BR-A7).
        JsonNode rods = analysis["machines"]!.AsArray()
            .Single(machine => machine!["id"]!.GetValue<string>().EndsWith("Build_ConstructorMk1_C_2147275293", StringComparison.Ordinal))!;
        Assert.Equal("starved", rods["limit"]!.GetValue<string>());
        Assert.Equal(0, rods["utilisation"]!.GetValue<double>());
        Assert.Equal("Recipe_IronRod_C", rods["recipe"]!.GetValue<string>());
        Assert.NotNull(rods["position"]!["x"]);
    }

    [Fact]
    public async Task The_findings_list_every_machine_the_running_ones_last()
    {
        JsonNode analysis = await AnalyseTestWorldAsync();

        List<(string Limit, double Utilisation)> machines = [.. analysis["machines"]!.AsArray()
            .Select(machine => (machine!["limit"]!.GetValue<string>(), machine["utilisation"]!.GetValue<double>()))];
        Assert.Equal(5, machines.Count);
        Assert.Equal([("none", 1.0), ("none", 1.0), ("none", 1.0)], machines[2..]);
    }

    [Fact]
    public async Task Unused_supply_says_what_a_provider_could_deliver_more()
    {
        JsonNode analysis = await AnalyseTestWorldAsync();

        JsonNode miner = analysis["unusedSupply"]!.AsArray()
            .Single(supply => supply!["id"]!.GetValue<string>().EndsWith("Build_MinerMk1_C_2147339296", StringComparison.Ordinal))!;
        Assert.Equal((60, 40), (miner["offered"]!.GetValue<double>(), miner["delivered"]!.GetValue<double>()));
    }

    [Fact]
    public async Task An_unknown_snapshot_has_no_analysis()
    {
        HttpResponseMessage response = await _client.GetAsync($"/api/v1/world/snapshots/{Guid.NewGuid()}/analysis", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<(Snapshot Snapshot, Factory Factory)> DefineIronPlatesAsync()
    {
        await _client.ImportAsync(CatalogClient.GameFile);
        await _client.ImportResourceNodesAsync();
        Snapshot snapshot = await _client.ImportSaveAsync(WorldClient.TestWorld);
        await _client.WaitForAnalysisAsync(snapshot.Id);
        HttpResponseMessage defined = await _client.DefineFactoryAsync("Development", "Iron plates", WorldClient.IronPlateMachines);
        return (snapshot, (await defined.Content.ReadFromJsonAsync<Factory>(TestContext.Current.CancellationToken))!);
    }

    [Fact]
    public async Task A_factory_analysis_says_what_it_makes_now_and_at_100_percent_and_where_it_goes()
    {
        (Snapshot snapshot, Factory factory) = await DefineIronPlatesAsync();

        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/world/snapshots/{snapshot.Id}/factories/{factory.Id}/analysis", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonNode analysis = (await response.Content.ReadFromJsonAsync<JsonNode>(TestContext.Current.CancellationToken))!;
        Assert.Equal(
            """[{"item":"Desc_IronPlate_C","now":15,"atFullSpeed":15,"receiversNeed":0,"othersBring":0,"short":0,"sunk":0,"cannotLeave":0,"destinations":[{"kind":"storage","factoryId":null,"factory":null,"building":null,"perMinute":15}]}]""",
            analysis["output"]!.ToJsonString());
        Assert.Equal(analysis["summary"]!["machines"]!.GetValue<int>(), analysis["machines"]!.AsArray().Count);
    }

    [Fact]
    public async Task A_factory_that_does_not_exist_has_no_analysis()
    {
        (Snapshot snapshot, _) = await DefineIronPlatesAsync();

        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/world/snapshots/{snapshot.Id}/factories/{Guid.NewGuid()}/analysis", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
