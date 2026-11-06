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
            """{"machines":5,"running":3,"starved":1,"outputBlocked":1,"noRecipe":0}""",
            analysis["summary"]!.ToJsonString());
    }

    [Fact]
    public async Task A_machine_below_100_percent_comes_with_its_cause_its_recipe_and_where_it_stands()
    {
        JsonNode analysis = await AnalyseTestWorldAsync();

        JsonNode smelterA = analysis["machines"]!.AsArray()
            .Single(machine => machine!["id"]!.GetValue<string>().EndsWith("Build_SmelterMk1_C_2147301226", StringComparison.Ordinal))!;
        Assert.Equal("outputBlocked", smelterA["limit"]!.GetValue<string>());
        Assert.Equal(0.4167, smelterA["utilisation"]!.GetValue<double>());
        Assert.Equal("Recipe_IngotIron_C", smelterA["recipe"]!.GetValue<string>());
        Assert.NotNull(smelterA["position"]!["x"]);
    }

    [Fact]
    public async Task Unused_supply_says_what_a_provider_could_deliver_more()
    {
        JsonNode analysis = await AnalyseTestWorldAsync();

        JsonNode miner = analysis["unusedSupply"]!.AsArray()
            .Single(supply => supply!["id"]!.GetValue<string>().EndsWith("Build_MinerMk1_C_2147339296", StringComparison.Ordinal))!;
        Assert.Equal((60, 22.5), (miner["offered"]!.GetValue<double>(), miner["delivered"]!.GetValue<double>()));
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
        HttpResponseMessage defined = await _client.DefineFactoryAsync("Development", "Iron plates", WorldClient.BeltIntoContainer, "Desc_OreIron_C");
        return (snapshot, (await defined.Content.ReadFromJsonAsync<Factory>(TestContext.Current.CancellationToken))!);
    }

    [Fact]
    public async Task A_factory_analysis_says_what_it_makes_now_and_at_100_percent()
    {
        (Snapshot snapshot, Factory factory) = await DefineIronPlatesAsync();

        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/world/snapshots/{snapshot.Id}/factories/{factory.Id}/analysis", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonNode analysis = (await response.Content.ReadFromJsonAsync<JsonNode>(TestContext.Current.CancellationToken))!;
        Assert.Equal("""[{"item":"Desc_IronPlate_C","now":15,"atFullSpeed":15}]""", analysis["output"]!.ToJsonString());
        Assert.Single(analysis["machines"]!.AsArray());
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
