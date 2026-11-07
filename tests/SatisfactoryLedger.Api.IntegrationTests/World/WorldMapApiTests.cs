using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

using SatisfactoryLedger.Api.IntegrationTests.Catalog;

namespace SatisfactoryLedger.Api.IntegrationTests.World;

/// <summary>GET /api/v1/world/snapshots/{id}/map: what the world map draws.</summary>
public sealed class WorldMapApiTests(LedgerApiFactory api) : IAsyncLifetime
{
    private readonly HttpClient _client = api.CreateClient();

    public async ValueTask InitializeAsync() => await api.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task The_map_of_a_snapshot_has_its_machines_and_providers_where_they_stand()
    {
        await _client.ImportAsync(CatalogClient.GameFile);
        await _client.ImportResourceNodesAsync();
        Snapshot snapshot = await _client.ImportSaveAsync(WorldClient.TestWorld);

        JsonNode map = (await _client.GetFromJsonAsync<JsonNode>($"/api/v1/world/snapshots/{snapshot.Id}/map", TestContext.Current.CancellationToken))!;

        Assert.Equal(5, map["machines"]!.AsArray().Count);
        JsonNode miner = map["providers"]!.AsArray()
            .Single(provider => provider!["id"]!.GetValue<string>().EndsWith("Build_MinerMk1_C_2147339296", StringComparison.Ordinal))!;
        Assert.Equal("Desc_OreIron_C", miner["resource"]!.GetValue<string>());
        Assert.NotNull(miner["position"]!["x"]);
    }

    [Fact]
    public async Task The_map_has_each_belt_with_its_curve()
    {
        await _client.ImportAsync(CatalogClient.GameFile);
        Snapshot snapshot = await _client.ImportSaveAsync(WorldClient.TestWorld);

        JsonNode map = (await _client.GetFromJsonAsync<JsonNode>($"/api/v1/world/snapshots/{snapshot.Id}/map", TestContext.Current.CancellationToken))!;

        JsonNode belt = map["conveyors"]!.AsArray()
            .Single(conveyor => conveyor!["id"]!.GetValue<string>().EndsWith("Build_ConveyorBeltMk1_C_2147303056", StringComparison.Ordinal))!;
        Assert.Equal("belt", belt["kind"]!.GetValue<string>());
        Assert.Equal(8, belt["curve"]!.AsArray().Count);
        Assert.NotNull(belt["curve"]![0]!["leaveTangent"]!["x"]);
        Assert.Equal(7, belt["contents"]!["Desc_OreIron_C"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task An_unknown_snapshot_has_no_map()
    {
        HttpResponseMessage response = await _client.GetAsync($"/api/v1/world/snapshots/{Guid.NewGuid()}/map", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
