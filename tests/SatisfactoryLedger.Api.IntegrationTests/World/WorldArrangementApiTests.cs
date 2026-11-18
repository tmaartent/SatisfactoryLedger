using System.Net;
using System.Net.Http.Json;

using SatisfactoryLedger.Api.IntegrationTests.Catalog;

namespace SatisfactoryLedger.Api.IntegrationTests.World;

/// <summary>
/// The optimal arrangement over the API (decided 2026-11-12): requested for a snapshot, computed by the analysis service
/// in the background, and every run kept per world.
/// </summary>
public sealed class WorldArrangementApiTests(LedgerApiFactory api) : IAsyncLifetime
{
    private readonly HttpClient _client = api.CreateClient();
    private Snapshot _snapshot = null!;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        await _client.ImportAsync(CatalogClient.GameFile);
        _snapshot = await _client.ImportSaveAsync(WorldClient.TestWorld);
        await _client.WaitForAnalysisAsync(_snapshot.Id);
        (await _client.DefineFactoryAsync("Development", "Smelting", WorldClient.IronPlateMachines[1])).EnsureSuccessStatusCode();
        (await _client.DefineFactoryAsync("Development", "Plates", WorldClient.IronPlateMachines[0])).EnsureSuccessStatusCode();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private Task<HttpResponseMessage> RequestAsync(object settings) =>
        _client.PostAsJsonAsync($"/api/v1/world/snapshots/{_snapshot.Id}/arrangements", settings, TestContext.Current.CancellationToken);

    private async Task<Arrangement> ReadyAsync(Guid id)
    {
        DateTime giveUp = DateTime.UtcNow.AddMinutes(1);
        while (DateTime.UtcNow < giveUp)
        {
            Arrangement arrangement = await _client.GetAsync<Arrangement>($"/api/v1/world/arrangements/{id}");
            if (arrangement.Summary.Status != "pending")
            {
                return arrangement;
            }

            await Task.Delay(200, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"Arrangement {id} wasn't ready after a minute");
    }

    [Fact]
    public async Task A_requested_arrangement_is_computed_in_the_background_and_kept()
    {
        HttpResponseMessage response = await RequestAsync(new { plotWidthMetres = 16, plotDepthMetres = 16, roadXMetres = 8, roadYMetres = 8, seconds = 5 });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        ArrangementSummary requested = (await response.Content.ReadFromJsonAsync<ArrangementSummary>(TestContext.Current.CancellationToken))!;
        Assert.Equal("pending", requested.Status);

        Arrangement ready = await ReadyAsync(requested.Id);
        Assert.Equal("ready", ready.Summary.Status);
        Assert.Equal(["Plates", "Smelting"], ready.Factories.Select(factory => factory.Name).Order(StringComparer.Ordinal));
        Assert.All(ready.Factories, factory => Assert.NotNull(factory.Proposed));
        Assert.True(ready.Summary.Cost <= ready.Summary.CostNow, $"{ready.Summary.Cost} > {ready.Summary.CostNow}");
        Assert.Contains(ready.Links, link => link.Belts > 0);
    }

    [Fact]
    public async Task Every_run_is_kept_for_its_world()
    {
        ArrangementSummary first = (await (await RequestAsync(new { seconds = 2 })).Content.ReadFromJsonAsync<ArrangementSummary>(TestContext.Current.CancellationToken))!;
        ArrangementSummary second = (await (await RequestAsync(new { plotWidthMetres = 40, together = 0, seconds = 2 })).Content.ReadFromJsonAsync<ArrangementSummary>(TestContext.Current.CancellationToken))!;

        ArrangementSummary[] runs = await _client.GetAsync<ArrangementSummary[]>("/api/v1/world/arrangements?world=Development");

        Assert.Equal([second.Id, first.Id], runs.Select(run => run.Id));
        Assert.Equal((48.0, 2, 40.0, 0), (first.PlotWidthMetres, first.Together, second.PlotWidthMetres, second.Together));
    }

    [Fact]
    public async Task Its_plots_must_have_a_size_and_its_roads_cannot_be_negative()
    {
        HttpResponseMessage response = await RequestAsync(new { plotWidthMetres = 0, roadXMetres = -1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Keeping_the_factories_together_cannot_weigh_less_than_nothing()
    {
        HttpResponseMessage response = await RequestAsync(new { together = -1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Its_exclusions_are_kept_with_the_run()
    {
        var trackside = new WorldRectangle(0, 0, 2000, 3000);

        HttpResponseMessage response = await RequestAsync(new { exclusions = new[] { trackside }, seconds = 2 });

        ArrangementSummary requested = (await response.Content.ReadFromJsonAsync<ArrangementSummary>(TestContext.Current.CancellationToken))!;
        Assert.Equal([trackside], (await ReadyAsync(requested.Id)).Summary.Exclusions);
    }

    [Fact]
    public async Task An_exclusion_must_have_some_size()
    {
        HttpResponseMessage response = await RequestAsync(new { exclusions = new[] { new WorldRectangle(0, 0, 0, 3000) } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_arrangement_of_an_unknown_snapshot_is_not_found()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"/api/v1/world/snapshots/{Guid.NewGuid()}/arrangements", new { }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
