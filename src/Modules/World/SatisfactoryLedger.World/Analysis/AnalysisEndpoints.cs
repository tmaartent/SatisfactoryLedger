using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

using SatisfactoryLedger.World.Persistence;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Analysis;

internal static class AnalysisEndpoints
{
    public static RouteGroupBuilder MapAnalysisEndpoints(this RouteGroupBuilder world)
    {
        world.MapGet("/snapshots/{id:guid}/analysis", AnalyseAsync)
            .WithName("AnalyseSnapshot")
            .WithSummary("The findings of a snapshot: machines below 100% and why, bottlenecks, unused supply, and sources of unknown rate")
            .WithDescription("Computed on request from the steady state of the snapshot's world (BR-A1 to A13); a big world takes some seconds. Rates per minute, to 4 decimals. Every list starts with the worst. A finding in a part of the world that didn't settle is marked approximate.");

        return world;
    }

    private static async Task<Results<Ok<AnalysisResponse>, NotFound>> AnalyseAsync(Guid id, WorldDbContext db, CancellationToken cancellationToken)
    {
        var snapshot = await db.LoadSnapshotAsync(id, cancellationToken);
        return snapshot is null ? TypedResults.NotFound() : TypedResults.Ok(SnapshotAnalysis.For(snapshot.Value.Document));
    }
}
