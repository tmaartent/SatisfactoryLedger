using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

using SatisfactoryLedger.Api.IntegrationTests.Catalog;

namespace SatisfactoryLedger.Api.IntegrationTests.World;

/// <summary>Where a shortage or a blockage starts, over the API (BR-A20, BR-A21, step 99), on a real PostgreSQL.</summary>
public sealed class WorldTraceApiTests(LedgerApiFactory api) : IAsyncLifetime
{
    private readonly HttpClient _client = api.CreateClient();
    private Guid _snapshot;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        await _client.ImportAsync(CatalogClient.GameFile);
        await _client.ImportResourceNodesAsync();
        Snapshot snapshot = await _client.ImportSaveAsync(WorldClient.TestWorld);
        await _client.WaitForAnalysisAsync(snapshot.Id);
        _snapshot = snapshot.Id;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task A_shortage_is_traced_from_the_starved_rod_constructor_to_where_it_starts()
    {
        JsonNode trace = await GetAsync("shortage?building=Build_ConstructorMk1_C&recipe=Recipe_IronRod_C");

        Assert.Equal(1, trace["machines"]!.GetValue<int>());
        Assert.Equal(1, trace["traced"]!.GetValue<int>());
        JsonNode start = Assert.Single(trace["starts"]!.AsArray())!;
        Assert.Equal("Desc_IronIngot_C", start["item"]!.GetValue<string>());
    }

    [Fact]
    public async Task Machines_that_are_not_held_back_have_nothing_to_trace()
    {
        JsonNode trace = await GetAsync("blockage?building=Build_SmelterMk1_C");

        Assert.Equal(2, trace["machines"]!.GetValue<int>());
        Assert.Equal(0, trace["traced"]!.GetValue<int>());
        Assert.Empty(trace["starts"]!.AsArray());
    }

    [Fact]
    public async Task A_trace_in_an_unknown_snapshot_is_not_found()
    {
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/world/snapshots/{Guid.NewGuid()}/traces/shortage?building=Build_ConstructorMk1_C",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_trace_needs_a_building()
    {
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/world/snapshots/{_snapshot}/traces/blockage", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<JsonNode> GetAsync(string trace)
    {
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/world/snapshots/{_snapshot}/traces/{trace}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonNode>(TestContext.Current.CancellationToken))!;
    }
}
