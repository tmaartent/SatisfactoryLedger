using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

using SatisfactoryLedger.Api.IntegrationTests.Catalog;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.Persistence;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.Api.IntegrationTests.World;

/// <summary>Importing a save into a world snapshot over the API (BR-W10, ADR-0016), on a real PostgreSQL.</summary>
public sealed class WorldSnapshotApiTests(LedgerApiFactory api) : IAsyncLifetime
{
    private readonly HttpClient _client = api.CreateClient();

    public async ValueTask InitializeAsync() => await api.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Importing_a_save_creates_a_snapshot_of_what_it_contains()
    {
        await _client.ImportAsync(CatalogClient.GameFile);

        HttpResponseMessage response = await _client.PostSaveAsync(WorldClient.TestWorld);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var snapshot = await response.Content.ReadFromJsonAsync<Snapshot>(TestContext.Current.CancellationToken);
        Assert.Equal("Development", snapshot!.SessionName);
        Assert.Equal(517554, snapshot.GameBuild);
        Assert.Equal(new SnapshotCounts(Machines: 5, Providers: 5, Consumers: 6, Logistics: 37, Connections: 46), snapshot.Counts);
        Assert.Empty(snapshot.UnknownRecipes);
        Assert.Empty(snapshot.Unsupported);
    }

    [Fact]
    public async Task An_imported_snapshot_has_the_catalog_rates_the_flow_analysis_needs()
    {
        await _client.ImportAsync(CatalogClient.GameFile);
        await _client.ImportResourceNodesAsync();
        HttpResponseMessage response = await _client.PostSaveAsync(WorldClient.TestWorld);
        var created = await response.Content.ReadFromJsonAsync<Snapshot>(TestContext.Current.CancellationToken);

        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        StoredSnapshot stored = (await scope.ServiceProvider.GetRequiredService<WorldDbContext>().Snapshots
            .FindAsync([created!.Id], TestContext.Current.CancellationToken))!;
        FlowNetwork network = SnapshotDocument.FromJson(stored.Content).ToFlowNetwork();

        FlowNode miner = network.Nodes[network.IndexOf[BuildableId.Of("Persistent_Level:PersistentLevel.Build_MinerMk1_C_2147339296")]];
        Assert.Equal(new FlowProvider(miner.Id, ItemId.Of("Desc_OreIron_C"), 60), miner);
        Assert.Contains(ItemId.Of("Desc_Water_C"), network.Fluids);
    }

    [Fact]
    public async Task A_snapshot_can_be_read_back_from_its_location()
    {
        await _client.ImportAsync(CatalogClient.GameFile);
        HttpResponseMessage response = await _client.PostSaveAsync(WorldClient.TestWorld);
        var created = await response.Content.ReadFromJsonAsync<Snapshot>(TestContext.Current.CancellationToken);

        var readBack = await _client.GetAsync<Snapshot>(response.Headers.Location!.ToString());

        Assert.Equal(created!.Id, readBack.Id);
        Assert.Equal(created.Counts, readBack.Counts);
    }

    [Fact]
    public async Task Every_import_is_kept_and_listed_newest_first()
    {
        await _client.ImportAsync(CatalogClient.GameFile);
        Snapshot first = await _client.ImportSaveAsync(WorldClient.TestWorld);
        Snapshot second = await _client.ImportSaveAsync(WorldClient.TestWorld);

        var snapshots = await _client.GetAsync<SnapshotSummary[]>("/api/v1/world/snapshots");

        Assert.Equal([second.Id, first.Id], snapshots.Select(snapshot => snapshot.Id));
    }

    [Fact]
    public async Task A_save_with_recipes_the_catalog_does_not_know_is_still_imported_and_lists_them()
    {
        // No catalog import, so the Catalog knows no recipe at all.
        HttpResponseMessage response = await _client.PostSaveAsync(WorldClient.TestWorld);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var snapshot = await response.Content.ReadFromJsonAsync<Snapshot>(TestContext.Current.CancellationToken);
        Assert.Equal(["Recipe_IngotIron_C", "Recipe_IronPlate_C", "Recipe_IronRod_C", "Recipe_PackagedWater_C"], snapshot!.UnknownRecipes);
        Assert.Equal(5, snapshot.Counts.Machines);
    }

    [Fact]
    public async Task A_file_that_is_not_a_save_is_unprocessable()
    {
        HttpResponseMessage response = await _client.PostSaveAsync("this is not a save file"u8.ToArray(), "notes.sav");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("World.InvalidSaveFile", problem!.Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task An_import_without_a_file_is_a_bad_request()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("nothing"), "comment");

        HttpResponseMessage response = await _client.PostAsync("/api/v1/world/snapshots", form, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_snapshot_is_not_found()
    {
        HttpResponseMessage response = await _client.GetAsync($"/api/v1/world/snapshots/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
