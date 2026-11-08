using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

using SatisfactoryLedger.World.Persistence;
using SatisfactoryLedger.World.Simulation;
using SatisfactoryLedger.World.Snapshots;

using Wolverine;
namespace SatisfactoryLedger.World.Analysis;

internal static class AnalysisEndpoints
{
    public const string AnalysisFailedCode = "World.AnalysisFailed";

    public static RouteGroupBuilder MapAnalysisEndpoints(this RouteGroupBuilder world)
    {
        world.MapGet("/snapshots/{id:guid}/analysis", AnalyseAsync)
            .WithName("AnalyseSnapshot")
            .WithSummary("The findings of a snapshot: every machine, why it runs below 100%, bottlenecks, unused supply, and sources of unknown rate")
            .WithDescription("Computed in the background after the import and stored (ADR-0017): 202 with status pending until it's ready. Rates per minute, to 4 decimals. Every list starts with the worst. A finding in a part of the world that didn't settle is marked approximate.")
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        world.MapGet("/snapshots/{id:guid}/factories/{factoryId:guid}/analysis", AnalyseFactoryAsync)
            .WithName("AnalyseFactory")
            .WithSummary("The findings of one factory in a snapshot: what it makes, its machines and belts, and the suppliers that feed it (BR-A14)")
            .WithDescription("Made from the snapshot's stored analysis: 202 with status pending until that's ready. Not found when the snapshot or factory doesn't exist, the factory belongs to another world, or its output belt isn't in this snapshot (BR-W11).")
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return world;
    }

    private static async Task<Results<Ok<AnalysisResponse>, Accepted<AnalysisPendingResponse>, NotFound, ProblemHttpResult>> AnalyseAsync(
        // [FromServices]: without it, the build-time OpenAPI generation (where Wolverine isn't there) takes it for a body.
        Guid id, WorldDbContext db, [FromServices] IMessageBus bus, CancellationToken cancellationToken)
    {
        var snapshot = await db.LoadSnapshotAsync(id, cancellationToken);
        if (snapshot is null)
        {
            return TypedResults.NotFound();
        }

        return await StoredAsync(id, db, bus, cancellationToken) switch
        {
            { Document: { } analysis } => TypedResults.Ok(SnapshotAnalysis.For(snapshot.Value.Document, analysis)),
            { Error: { } error } => Failed(error),
            _ => Pending(id),
        };
    }

    private static async Task<Results<Ok<FactoryAnalysisResponse>, Accepted<AnalysisPendingResponse>, NotFound, ProblemHttpResult>> AnalyseFactoryAsync(
        Guid id, Guid factoryId, WorldDbContext db, [FromServices] IMessageBus bus, CancellationToken cancellationToken)
    {
        var snapshot = await db.LoadSnapshotAsync(id, cancellationToken);
        Factories.StoredFactory? factory = await db.Factories.FindAsync([factoryId], cancellationToken);
        if (snapshot is null || factory is null || factory.World != snapshot.Value.World)
        {
            return TypedResults.NotFound();
        }

        switch (await StoredAsync(id, db, bus, cancellationToken))
        {
            case { Document: { } analysis }:
                FactoryAnalysisResponse? findings = SnapshotAnalysis.ForFactory(snapshot.Value.Document, factory, analysis);
                return findings is null ? TypedResults.NotFound() : TypedResults.Ok(findings);
            case { Error: { } error }:
                return Failed(error);
            default:
                return Pending(id);
        }
    }

    /// <summary>The snapshot's analysis when it's current, or why it failed. Queues it when it's missing or outdated.</summary>
    private static async Task<(AnalysisDocument? Document, string? Error)> StoredAsync(
        Guid id, WorldDbContext db, IMessageBus bus, CancellationToken cancellationToken)
    {
        StoredAnalysis? stored = await db.Analyses.AsNoTracking().SingleOrDefaultAsync(analysis => analysis.SnapshotId == id, cancellationToken);
        if (stored is { IsCurrent: true, Content: { } content })
        {
            return (AnalysisDocument.FromJson(content), null);
        }

        if (stored is { Status: AnalysisStatus.Failed, Version: 0 or AnalysisDocument.CurrentVersion } && stored.Error is not null)
        {
            return (null, stored.Error);
        }

        // Imported before analyses were stored, or computed with older rules: ask the analysis service (ADR-0018).
        if (stored is null || (stored.Status != AnalysisStatus.Pending && stored.Version != AnalysisDocument.CurrentVersion))
        {
            await bus.PublishAsync(new SnapshotImported(id));
        }

        return (null, null);
    }

    private static Accepted<AnalysisPendingResponse> Pending(Guid id) =>
        TypedResults.Accepted($"/api/v1/world/snapshots/{id}/analysis", new AnalysisPendingResponse(AnalysisStatus.Pending));

    private static ProblemHttpResult Failed(string error) => TypedResults.Problem(
        title: "The analysis of this snapshot failed",
        detail: error,
        statusCode: StatusCodes.Status500InternalServerError,
        extensions: new Dictionary<string, object?> { ["code"] = AnalysisFailedCode });
}