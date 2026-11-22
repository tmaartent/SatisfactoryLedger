using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

using SatisfactoryLedger.World.Analysis;
using SatisfactoryLedger.World.Persistence;
using SatisfactoryLedger.World.Snapshots;

using Wolverine;

namespace SatisfactoryLedger.World.Tracing;

/// <summary>Where a shortage or an output blockage starts (decided 2026-11-20), over a snapshot's stored analysis.</summary>
internal static class TraceEndpoints
{
    public static RouteGroupBuilder MapTraceEndpoints(this RouteGroupBuilder world)
    {
        world.MapGet("/snapshots/{id:guid}/traces/shortage", ShortageAsync)
            .WithName("TraceShortage")
            .WithSummary("Where the shortage of the starved machines of one class and recipe starts: upstream along what they're short of")
            .WithDescription("?building=<class name>&recipe=<class name, optional>. Each start says why (too few producers, a belt full, a source at its limit, taken by others, …), of which item, at which buildables, and the steps leading there. 202 until the analysis is ready.")
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        world.MapGet("/snapshots/{id:guid}/traces/blockage", BlockageAsync)
            .WithName("TraceBlockage")
            .WithSummary("Where the blockage of the output-blocked machines of one class and recipe starts: downstream along what can't leave")
            .WithDescription("?building=<class name>&recipe=<class name, optional>. Each start says why (too few consumers, a belt full, consumers starved, storage full, nowhere to go, …), of which item, at which buildables, and the steps leading there. 202 until the analysis is ready.")
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return world;
    }

    private static Task<Results<Ok<TraceResponse>, Accepted<AnalysisPendingResponse>, NotFound, ProblemHttpResult>> ShortageAsync(
        Guid id, string building, string? recipe, WorldDbContext db, [FromServices] IMessageBus bus, CancellationToken cancellationToken) =>
        TraceAsync(id, db, bus, (snapshot, analysis) => SnapshotTraces.Shortage(snapshot, analysis, building, recipe), cancellationToken);

    private static Task<Results<Ok<TraceResponse>, Accepted<AnalysisPendingResponse>, NotFound, ProblemHttpResult>> BlockageAsync(
        Guid id, string building, string? recipe, WorldDbContext db, [FromServices] IMessageBus bus, CancellationToken cancellationToken) =>
        TraceAsync(id, db, bus, (snapshot, analysis) => SnapshotTraces.Blockage(snapshot, analysis, building, recipe), cancellationToken);

    private static async Task<Results<Ok<TraceResponse>, Accepted<AnalysisPendingResponse>, NotFound, ProblemHttpResult>> TraceAsync(
        Guid id,
        WorldDbContext db,
        IMessageBus bus,
        Func<SnapshotDocument, Contracts.Analysis.AnalysisDocument, TraceResponse> trace,
        CancellationToken cancellationToken)
    {
        var snapshot = await db.LoadSnapshotAsync(id, cancellationToken);
        if (snapshot is null)
        {
            return TypedResults.NotFound();
        }

        return await AnalysisEndpoints.StoredAsync(id, db, bus, cancellationToken) switch
        {
            { Document: { } analysis } => TypedResults.Ok(trace(snapshot.Value.Document, analysis)),
            { Error: { } error } => AnalysisEndpoints.Failed(error),
            _ => AnalysisEndpoints.Pending(id),
        };
    }
}
