using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SatisfactoryLedger.Api.IntegrationTests.Catalog;
using SatisfactoryLedger.World.Analysis;
using SatisfactoryLedger.World.Persistence;
using SatisfactoryLedger.World.Contracts.Analysis;
using SatisfactoryLedger.World.Simulation;

namespace SatisfactoryLedger.Api.IntegrationTests.World;

/// <summary>The endpoints the analysis service calls (ADR-0018): the flow network out, the analysis back in.</summary>
public sealed class WorldInternalApiTests(LedgerApiFactory api) : IAsyncLifetime
{
    private readonly HttpClient _client = api.CreateClient();

    public async ValueTask InitializeAsync() => await api.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>An imported test world, copied with a pending analysis the background doesn't know about.</summary>
    private async Task<Guid> PendingSnapshotAsync()
    {
        await _client.ImportAsync(CatalogClient.GameFile);
        Snapshot imported = await _client.ImportSaveAsync(WorldClient.TestWorld);

        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
        StoredSnapshot source = await db.Snapshots.AsNoTracking().SingleAsync(snapshot => snapshot.Id == imported.Id, TestContext.Current.CancellationToken);
        var copy = new StoredSnapshot
        {
            Id = Guid.CreateVersion7(), SessionName = source.SessionName, GameBuild = source.GameBuild, SavedAt = source.SavedAt,
            ImportedAt = source.ImportedAt, Counts = source.Counts, UnknownRecipes = source.UnknownRecipes, Unsupported = source.Unsupported,
            Content = source.Content,
        };
        db.Snapshots.Add(copy);
        db.Analyses.Add(StoredAnalysis.PendingFor(copy.Id));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return copy.Id;
    }

    private async Task<FlowNetworkDocument> FlowNetworkAsync(Guid id)
    {
        HttpResponseMessage response = await _client.GetAsync($"/internal/world/snapshots/{id}/flow-network", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return FlowNetworkDocument.FromJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_flow_network_of_a_snapshot_has_every_node_of_its_world()
    {
        Guid id = await PendingSnapshotAsync();

        FlowNetworkDocument network = await FlowNetworkAsync(id);

        Assert.Equal(5, network.Nodes.OfType<MachineDocument>().Count());
        Assert.Contains("Desc_Water_C", network.Fluids);
    }

    [Fact]
    public async Task An_analysis_sent_back_is_stored_and_the_findings_are_ready()
    {
        Guid id = await PendingSnapshotAsync();
        AnalysisDocument analysis = AnalysisDocument.Compute((await FlowNetworkAsync(id)).ToNetwork());
        string result = System.Text.Json.JsonSerializer.Serialize(new AnalysisResult(TimeSpan.FromSeconds(1), analysis), SimulationJson.Options);

        HttpResponseMessage stored = await _client.PutAsync(
            $"/internal/world/snapshots/{id}/analysis", new StringContent(result, System.Text.Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);
        HttpResponseMessage findings = await _client.GetAsync($"/api/v1/world/snapshots/{id}/analysis", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, stored.StatusCode);
        Assert.Equal(HttpStatusCode.OK, findings.StatusCode);
    }

    [Fact]
    public async Task A_failure_sent_back_is_stored_with_its_reason()
    {
        Guid id = await PendingSnapshotAsync();

        HttpResponseMessage stored = await _client.PutAsJsonAsync(
            $"/internal/world/snapshots/{id}/analysis/failure", new { error = "The service ran out of patience" }, TestContext.Current.CancellationToken);
        HttpResponseMessage findings = await _client.GetAsync($"/api/v1/world/snapshots/{id}/analysis", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, stored.StatusCode);
        JsonNode problem = (await findings.Content.ReadFromJsonAsync<JsonNode>(TestContext.Current.CancellationToken))!;
        Assert.Equal("The service ran out of patience", problem["detail"]!.GetValue<string>());
    }

    [Fact]
    public async Task An_unknown_snapshot_has_no_flow_network()
    {
        HttpResponseMessage response = await _client.GetAsync($"/internal/world/snapshots/{Guid.NewGuid()}/flow-network", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_internal_endpoints_are_not_in_the_public_contract()
    {
        string openApi = await _client.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        Assert.DoesNotContain("/internal/", openApi, StringComparison.Ordinal);
    }
}
