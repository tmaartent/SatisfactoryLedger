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
    public async Task The_belts_between_the_arranged_factories_are_routed_on_the_floor_below_them()
    {
        ArrangementSummary requested = (await (await RequestAsync(new { plotWidthMetres = 16, plotDepthMetres = 16, roadXMetres = 8, roadYMetres = 8, ceilingMetres = 12, seconds = 5 }))
            .Content.ReadFromJsonAsync<ArrangementSummary>(TestContext.Current.CancellationToken))!;

        Arrangement ready = await ReadyAsync(requested.Id);

        HashSet<string> arranged = [.. ready.Factories.Select(factory => factory.FactoryId.ToString())];
        int belts = ready.Links.Where(link => arranged.Contains(link.From) && arranged.Contains(link.To)).Sum(link => link.Belts);
        Assert.Equal(12, ready.Summary.CeilingMetres);
        Assert.Equal((belts, 0), (ready.Belts.Count(belt => arranged.Contains(belt.From) && arranged.Contains(belt.To)), ready.Summary.UnroutedBelts));

        // Smelting's ore comes up from its miner's spot.
        Assert.Contains(ready.Belts, belt => !arranged.Contains(belt.From) && arranged.Contains(belt.To));
        Assert.All(ready.Belts, belt => Assert.True(belt.Corners.Length >= 2));

        // Its level on each straight run, from 0 on the floor.
        Assert.All(ready.Belts, belt => Assert.Equal(belt.Corners.Length - 1, belt.Levels.Length));

        // Each says what it carries.
        Assert.All(ready.Belts, belt => Assert.True(belt.Item is not null && belt.PerMinute > 0, $"{belt.From} → {belt.To} carries {belt.Item} at {belt.PerMinute}"));
        Assert.True(ready.Summary.BeltMetres > 0, $"{ready.Summary.BeltMetres} metres of belt");

        // The levels it says it uses are those the 3D view shows: up to its highest belt.
        Assert.Equal(ready.Belts.Max(belt => belt.Levels.Max()) + 1, ready.Summary.LevelsUsed);

        // Routed cleanly: no belt needed a last-resort crossing (step 102).
        Assert.Equal(0, ready.Summary.LastResortCrossings);
        Assert.All(ready.Belts, belt => Assert.Empty(belt.LastResortCrossings));
    }

    [Fact]
    public async Task With_supply_lines_the_belts_are_routed_as_lines_and_say_what_they_carry()
    {
        ArrangementSummary requested = (await (await RequestAsync(new { plotWidthMetres = 16, plotDepthMetres = 16, roadXMetres = 8, roadYMetres = 8, supplyLines = true, seconds = 5 }))
            .Content.ReadFromJsonAsync<ArrangementSummary>(TestContext.Current.CancellationToken))!;

        Arrangement ready = await ReadyAsync(requested.Id);

        Assert.True(ready.Summary.SupplyLines);
        Assert.Equal(0, ready.Summary.UnroutedBelts);
        // Every belt routed at the first placement: no other was tried (step 101).
        Assert.Equal(1, ready.Summary.Placements);
        Assert.Contains(ready.Belts, belt => belt.Item == "Desc_IronIngot_C");
        Assert.All(ready.Belts, belt => Assert.True(belt.Item is not null && belt.PerMinute > 0 && !belt.Injection, $"{belt.From} → {belt.To} carries {belt.Item} at {belt.PerMinute}"));
    }

    [Fact]
    public async Task On_footprints_the_factories_are_arranged_on_the_foundations_their_machines_cover()
    {
        ArrangementSummary requested = (await (await RequestAsync(new { roadXMetres = 8, roadYMetres = 8, footprints = true, seconds = 5 }))
            .Content.ReadFromJsonAsync<ArrangementSummary>(TestContext.Current.CancellationToken))!;

        Arrangement ready = await ReadyAsync(requested.Id);

        Assert.Equal("ready", ready.Summary.Status);
        Assert.True(ready.Summary.Footprints);
        Assert.Equal(0, ready.Summary.UnroutedBelts);
        Assert.All(ready.Factories, factory => Assert.NotNull(factory.Proposed));
    }

    [Fact]
    public async Task The_machines_of_a_runs_factories_come_with_their_footprints_as_they_stand_now()
    {
        ArrangementSummary requested = (await (await RequestAsync(new { plotWidthMetres = 16, plotDepthMetres = 16, roadXMetres = 8, roadYMetres = 8, seconds = 5 }))
            .Content.ReadFromJsonAsync<ArrangementSummary>(TestContext.Current.CancellationToken))!;
        Arrangement ready = await ReadyAsync(requested.Id);

        ArrangementMachines machines = await _client.GetAsync<ArrangementMachines>($"/api/v1/world/arrangements/{requested.Id}/machines");

        Assert.Equal(
            ready.Factories.Select(factory => factory.FactoryId).Order(),
            machines.Machines.Select(machine => machine.FactoryId).Distinct().Order());
        Assert.All(machines.Machines, machine => Assert.Contains(machines.Footprints, footprint => footprint.Id == machine.ClassName));
    }

    [Fact]
    public async Task The_machines_of_an_unknown_run_are_not_found()
    {
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/world/arrangements/{Guid.CreateVersion7()}/machines", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Belts_to_nodes_and_stations_cannot_weigh_below_nothing()
    {
        HttpResponseMessage response = await RequestAsync(new { spotWeight = -1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_ceiling_must_leave_room_for_a_belt()
    {
        HttpResponseMessage response = await RequestAsync(new { ceilingMetres = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Every_run_is_kept_for_its_world()
    {
        ArrangementSummary first = (await (await RequestAsync(new { seconds = 2 })).Content.ReadFromJsonAsync<ArrangementSummary>(TestContext.Current.CancellationToken))!;
        ArrangementSummary second = (await (await RequestAsync(new { plotWidthMetres = 40, together = 0, seconds = 2 })).Content.ReadFromJsonAsync<ArrangementSummary>(TestContext.Current.CancellationToken))!;

        ArrangementSummary[] runs = await _client.GetAsync<ArrangementSummary[]>("/api/v1/world/arrangements?world=Development");

        Assert.Equal([second.Id, first.Id], runs.Select(run => run.Id));
        Assert.Equal((48.0, 2, 32.0, 40.0, 0), (first.PlotWidthMetres, first.Together, first.CeilingMetres, second.PlotWidthMetres, second.Together));
    }

    [Fact]
    public async Task A_run_can_be_deleted_even_while_it_is_computed()
    {
        ArrangementSummary kept = (await (await RequestAsync(new { seconds = 2 })).Content.ReadFromJsonAsync<ArrangementSummary>(TestContext.Current.CancellationToken))!;
        ArrangementSummary deleted = (await (await RequestAsync(new { seconds = 2 })).Content.ReadFromJsonAsync<ArrangementSummary>(TestContext.Current.CancellationToken))!;

        HttpResponseMessage response = await _client.DeleteAsync($"/api/v1/world/arrangements/{deleted.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal([kept.Id], (await _client.GetAsync<ArrangementSummary[]>("/api/v1/world/arrangements?world=Development")).Select(run => run.Id));
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(new Uri($"/api/v1/world/arrangements/{deleted.Id}", UriKind.Relative), TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task A_run_without_a_free_plot_for_every_factory_is_refused_with_the_reason()
    {
        // Plots of 100 km: the area holds one, and the freight platform takes it; two factories.
        HttpResponseMessage response = await RequestAsync(new { plotWidthMetres = 100_000, plotDepthMetres = 100_000 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string problem = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("2 factories but 0 free plots", problem, StringComparison.Ordinal);
        Assert.Empty(await _client.GetAsync<ArrangementSummary[]>("/api/v1/world/arrangements?world=Development"));
    }

    [Fact]
    public async Task Deleting_an_unknown_run_is_not_found()
    {
        HttpResponseMessage response = await _client.DeleteAsync($"/api/v1/world/arrangements/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
