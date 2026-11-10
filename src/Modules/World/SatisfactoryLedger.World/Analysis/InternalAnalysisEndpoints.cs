using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

using SatisfactoryLedger.World.Persistence;
using SatisfactoryLedger.World.Contracts.Analysis;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Analysis;

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