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

        world.MapGet("/snapshots/{id:guid}/factories/{factoryId:guid}/analysis", AnalyseFactoryAsync)
            .WithName("AnalyseFactory")
            .WithSummary("The findings of one factory in a snapshot: what it makes, its machines and belts, and the suppliers that feed it (BR-A14)")
            .WithDescription("The whole world is simulated, because a factory depends on its supply; a big world takes some seconds. Not found when the snapshot or factory doesn't exist, the factory belongs to another world, or its output belt isn't in this snapshot (BR-W11).");

        return world;
    }

    private static async Task<Results<Ok<FactoryAnalysisResponse>, NotFound>> AnalyseFactoryAsync(
        Guid id, Guid factoryId, WorldDbContext db, CancellationToken cancellationToken)
    {
        var snapshot = await db.LoadSnapshotAsync(id, cancellationToken);
        Factories.StoredFactory? factory = await db.Factories.FindAsync([factoryId], cancellationToken);
        if (snapshot is null || factory is null || factory.World != snapshot.Value.World)
        {
            return TypedResults.NotFound();
        }

        FactoryAnalysisResponse? analysis = SnapshotAnalysis.ForFactory(snapshot.Value.Document, factory);
        return analysis is null ? TypedResults.NotFound() : TypedResults.Ok(analysis);
    }

    private static async Task<Results<Ok<AnalysisResponse>, NotFound>> AnalyseAsync(Guid id, WorldDbContext db, CancellationToken cancellationToken)
    {
        var snapshot = await db.LoadSnapshotAsync(id, cancellationToken);
        return snapshot is null ? TypedResults.NotFound() : TypedResults.Ok(SnapshotAnalysis.For(snapshot.Value.Document));
    }
}
