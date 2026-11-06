using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SatisfactoryLedger.Api.IntegrationTests.Catalog;
using SatisfactoryLedger.World.Analysis;
using SatisfactoryLedger.World.Persistence;
using SatisfactoryLedger.World.Simulation;

namespace SatisfactoryLedger.Api.IntegrationTests.World;

/// <summary>A snapshot's analysis, computed in the background after the import and stored (ADR-0017).</summary>
public sealed class WorldAnalysisStorageTests(LedgerApiFactory api) : IAsyncLifetime
{
    private readonly HttpClient _client = api.CreateClient();

    public async ValueTask InitializeAsync() => await api.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<Snapshot> ImportTestWorldAsync()
    {
        await _client.ImportAsync(CatalogClient.GameFile);
        return await _client.ImportSaveAsync(WorldClient.TestWorld);
    }

    /// <summary>
    /// A copy of an imported snapshot with an analysis in the given state, written straight to the database: the
    /// worker isn't told about it, so it stays as it is until the API asks.
    /// </summary>
    private async Task<Guid> CopyWithAnalysisAsync(Guid original, Func<Guid, StoredAnalysis> analysisFor)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
        StoredSnapshot source = await db.Snapshots.AsNoTracking().SingleAsync(snapshot => snapshot.Id == original, TestContext.Current.CancellationToken);
        var copy = new StoredSnapshot
        {
            Id = Guid.CreateVersion7(),
            SessionName = source.SessionName,
            GameBuild = source.GameBuild,
            SavedAt = source.SavedAt,
            ImportedAt = source.ImportedAt,
            Counts = source.Counts,
            UnknownRecipes = source.UnknownRecipes,
            Unsupported = source.Unsupported,
            Content = source.Content,
        };
        db.Snapshots.Add(copy);
        db.Analyses.Add(analysisFor(copy.Id));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return copy.Id;
    }

    [Fact]
    public async Task A_new_snapshot_says_its_analysis_is_pending()
    {
        Snapshot snapshot = await ImportTestWorldAsync();

        Assert.Equal("pending", snapshot.Analysis);
    }

    [Fact]
    public async Task The_analysis_is_computed_in_the_background_and_stored_with_its_version()
    {
        Snapshot snapshot = await ImportTestWorldAsync();

        await _client.WaitForAnalysisAsync(snapshot.Id);

        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        StoredAnalysis stored = await scope.ServiceProvider.GetRequiredService<WorldDbContext>().Analyses
            .SingleAsync(analysis => analysis.SnapshotId == snapshot.Id, TestContext.Current.CancellationToken);
        Assert.Equal((AnalysisStatus.Ready, AnalysisDocument.CurrentVersion), (stored.Status, stored.Version));
        Assert.NotNull(stored.Content);
        Assert.NotNull(stored.Duration);
    }

    [Fact]
    public async Task While_the_analysis_is_pending_the_api_says_so_with_202()
    {
        Snapshot imported = await ImportTestWorldAsync();
        Guid pending = await CopyWithAnalysisAsync(imported.Id, StoredAnalysis.PendingFor);

        HttpResponseMessage response = await _client.GetAsync($"/api/v1/world/snapshots/{pending}/analysis", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        JsonNode body = (await response.Content.ReadFromJsonAsync<JsonNode>(TestContext.Current.CancellationToken))!;
        Assert.Equal("pending", body["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_failed_analysis_is_a_problem_with_its_reason()
    {
        Snapshot imported = await ImportTestWorldAsync();
        Guid failed = await CopyWithAnalysisAsync(imported.Id, id => new StoredAnalysis
        {
            SnapshotId = id,
            Status = AnalysisStatus.Failed,
            Version = AnalysisDocument.CurrentVersion,
            Error = "Something went wrong",
        });

        HttpResponseMessage response = await _client.GetAsync($"/api/v1/world/snapshots/{failed}/analysis", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        JsonNode problem = (await response.Content.ReadFromJsonAsync<JsonNode>(TestContext.Current.CancellationToken))!;
        Assert.Equal(("World.AnalysisFailed", "Something went wrong"), (problem["code"]!.GetValue<string>(), problem["detail"]!.GetValue<string>()));
    }

    [Fact]
    public async Task At_startup_every_snapshot_without_a_current_analysis_is_analysed_again()
    {
        Snapshot imported = await ImportTestWorldAsync();
        await _client.WaitForAnalysisAsync(imported.Id);
        Guid outdated = await CopyWithAnalysisAsync(imported.Id, id => new StoredAnalysis
        {
            SnapshotId = id, Status = AnalysisStatus.Ready, Version = AnalysisDocument.CurrentVersion - 1, Content = "{}",
        });
        Guid failed = await CopyWithAnalysisAsync(imported.Id, id => new StoredAnalysis { SnapshotId = id, Status = AnalysisStatus.Failed, Error = "Restart" });

        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        List<Guid> toAnalyse = await AnalysisRequeue.ToAnalyseAsync(scope.ServiceProvider.GetRequiredService<WorldDbContext>(), TestContext.Current.CancellationToken);

        Assert.Equal(new HashSet<Guid> { outdated, failed }, toAnalyse.ToHashSet());
    }

    [Fact]
    public async Task An_API_that_starts_with_snapshots_to_analyse_keeps_running_and_has_them_analysed()
    {
        Snapshot imported = await ImportTestWorldAsync();
        await _client.WaitForAnalysisAsync(imported.Id);
        Guid outdated = await CopyWithAnalysisAsync(imported.Id, id => new StoredAnalysis
        {
            SnapshotId = id, Status = AnalysisStatus.Ready, Version = AnalysisDocument.CurrentVersion - 1, Content = "{}",
        });
        Guid failed = await CopyWithAnalysisAsync(imported.Id, id => new StoredAnalysis { SnapshotId = id, Status = AnalysisStatus.Failed, Error = "Restart" });

        // A second API on the same database and broker: what a restart does.
        await using WebApplicationFactory<Program> restarted = api.WithWebHostBuilder(_ => { });
        HttpClient client = restarted.CreateClient();

        await WaitUntilCurrentAsync(outdated, failed);
        HttpResponseMessage stillRunning = await client.GetAsync("/api/v1/world/snapshots", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, stillRunning.StatusCode);
    }

    private async Task WaitUntilCurrentAsync(params Guid[] snapshots)
    {
        DateTime giveUp = DateTime.UtcNow.AddMinutes(1);
        while (DateTime.UtcNow < giveUp)
        {
            await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
            List<StoredAnalysis> analyses = await db.Analyses.AsNoTracking()
                .Where(analysis => snapshots.Contains(analysis.SnapshotId)).ToListAsync(TestContext.Current.CancellationToken);
            if (analyses.Count == snapshots.Length && analyses.All(analysis => analysis.IsCurrent))
            {
                return;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException("The snapshots weren't analysed again within a minute");
    }
}
