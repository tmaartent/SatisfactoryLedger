using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

using SatisfactoryLedger.World.Persistence;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Map;

internal static class WorldMapEndpoints
{
    public static RouteGroupBuilder MapWorldMapEndpoints(this RouteGroupBuilder world)
    {
        world.MapGet("/snapshots/{id:guid}/map", GetAsync)
            .WithName("GetWorldMap")
            .WithSummary("What the world map draws of a snapshot: its machines and providers, where they stand")
            .WithDescription("Positions are in the game's units (centimetres). How each machine runs comes from the snapshot's analysis.");

        return world;
    }

    private static async Task<Results<Ok<WorldMapResponse>, NotFound>> GetAsync(Guid id, WorldDbContext db, CancellationToken cancellationToken)
    {
        var snapshot = await db.LoadSnapshotAsync(id, cancellationToken);
        return snapshot is null ? TypedResults.NotFound() : TypedResults.Ok(WorldMap.For(snapshot.Value.Document));
    }
}
