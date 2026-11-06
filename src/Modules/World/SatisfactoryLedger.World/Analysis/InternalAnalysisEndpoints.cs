using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

using SatisfactoryLedger.World.Persistence;
using SatisfactoryLedger.World.Simulation;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Analysis;

/// <param name="Error">Why the analysis service couldn't analyse the snapshot.</param>
internal sealed record AnalysisFailureRequest(string? Error);

/// <summary>
/// What the analysis service calls (ADR-0018): it fetches a snapshot's flow network and sends the analysis back.
/// Not part of the public API: these routes are left out of the OpenAPI document, and aren't for clients.
/// </summary>
internal static class InternalAnalysisEndpoints
{
    public static RouteGroupBuilder MapInternalAnalysisEndpoints(this RouteGroupBuilder world)
    {
        world.MapGet("/snapshots/{id:guid}/flow-network", GetFlowNetworkAsync);
        world.MapPut("/snapshots/{id:guid}/analysis", StoreAnalysisAsync);
        world.MapPut("/snapshots/{id:guid}/analysis/failure", StoreFailureAsync);
        return world;
    }

    private static async Task<Results<ContentHttpResult, NotFound>> GetFlowNetworkAsync(Guid id, WorldDbContext db, CancellationToken cancellationToken)
    {
        var snapshot = await db.LoadSnapshotAsync(id, cancellationToken);
        return snapshot is null
            ? TypedResults.NotFound()
            : TypedResults.Text(FlowNetworkDocument.From(snapshot.Value.Document.ToFlowNetwork()).ToJson(), "application/json");
    }

    private static async Task<Results<NoContent, NotFound>> StoreAnalysisAsync(
        Guid id, AnalysisResult result, StoreAnalysisHandler handler, CancellationToken cancellationToken) =>
        await handler.StoreAsync(id, result, cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound();

    private static async Task<Results<NoContent, NotFound>> StoreFailureAsync(
        Guid id, AnalysisFailureRequest failure, StoreAnalysisHandler handler, CancellationToken cancellationToken) =>
        await handler.StoreFailureAsync(id, failure.Error ?? "The analysis service gave no reason", cancellationToken)
            ? TypedResults.NoContent()
            : TypedResults.NotFound();
}

/// <summary>Stores what the analysis service sends back for a snapshot (ADR-0017, ADR-0018).</summary>
internal sealed class StoreAnalysisHandler(WorldDbContext db, TimeProvider clock)
{
    /// <returns>False when there's no such snapshot.</returns>
    public async Task<bool> StoreAsync(Guid snapshotId, AnalysisResult result, CancellationToken cancellationToken) =>
        await StoreAsync(snapshotId, stored =>
        {
            stored.Status = AnalysisStatus.Ready;
            stored.Version = result.Analysis.Version;
            stored.Content = result.Analysis.ToJson();
            stored.Duration = result.Duration;
            stored.Error = null;
        }, cancellationToken);

    /// <returns>False when there's no such snapshot.</returns>
    public async Task<bool> StoreFailureAsync(Guid snapshotId, string error, CancellationToken cancellationToken) =>
        await StoreAsync(snapshotId, stored =>
        {
            stored.Status = AnalysisStatus.Failed;
            stored.Version = AnalysisDocument.CurrentVersion;
            stored.Error = error;
        }, cancellationToken);

    private async Task<bool> StoreAsync(Guid snapshotId, Action<StoredAnalysis> update, CancellationToken cancellationToken)
    {
        if (await db.Snapshots.FindAsync([snapshotId], cancellationToken) is null)
        {
            return false;
        }

        StoredAnalysis? stored = await db.Analyses.FindAsync([snapshotId], cancellationToken);
        if (stored is null)
        {
            stored = StoredAnalysis.PendingFor(snapshotId);
            db.Analyses.Add(stored);
        }

        update(stored);
        stored.ComputedAt = clock.GetUtcNow();
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        // The snapshot was deleted meanwhile, taking its analysis with it: as if it was never there.
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}
