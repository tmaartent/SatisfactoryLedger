using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

using SatisfactoryLedger.Catalog.Browsing.Dto.Responses;
using SatisfactoryLedger.Catalog.Domain.Map;
using SatisfactoryLedger.Catalog.Persistence;

namespace SatisfactoryLedger.Catalog.Browsing;

/// <summary>The game's map picture for the map (ADR-0020): where it lies in the world, and each tile's image.</summary>
internal static class MapArtEndpoints
{
    /// <summary>A day: the tiles change only with an import, and the map asks for them on every visit.</summary>
    private const string TileCaching = "private, max-age=86400";

    public static RouteGroupBuilder MapMapArtEndpoints(this RouteGroupBuilder catalog)
    {
        catalog.MapGet("/map-art", GetAsync)
            .WithName("GetMapArt")
            .WithSummary("The game's map picture: the rectangle of the world it covers, and its tiles (404 before an import)");

        catalog.MapGet("/map-art/tiles/{column:int}/{row:int}", GetTileAsync)
            .WithName("GetMapArtTile")
            .WithSummary("One tile of the map picture, as the image it is");

        return catalog;
    }

    private static async Task<Results<Ok<MapArtResponse>, NotFound>> GetAsync(CatalogDbContext db, CancellationToken cancellationToken)
    {
        // Only where the tiles lie: their images stay in the database.
        var art = await db.MapArts.AsNoTracking()
            .Select(a => new
            {
                a.GameVersion,
                a.X0,
                a.Y0,
                a.Width,
                a.Height,
                Tiles = a.Tiles.Select(t => new { t.Column, t.Row }).ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (art is null)
        {
            return TypedResults.NotFound();
        }

        var world = new MapArtBounds(art.X0, art.Y0, art.X0 + art.Width, art.Y0 + art.Height);
        int columns = art.Tiles.Max(t => t.Column) + 1;
        int rows = art.Tiles.Max(t => t.Row) + 1;
        return TypedResults.Ok(new MapArtResponse(
            art.GameVersion,
            world.X0,
            world.Y0,
            world.X1,
            world.Y1,
            [
                .. art.Tiles.OrderBy(t => t.Row).ThenBy(t => t.Column).Select(t =>
                {
                    MapArtBounds tile = world.Tile(columns, rows, t.Column, t.Row);
                    return new MapArtTileResponse(t.Column, t.Row, tile.X0, tile.Y0, tile.X1, tile.Y1, $"/api/v1/catalog/map-art/tiles/{t.Column}/{t.Row}");
                })
            ]));
    }

    private static async Task<Results<FileContentHttpResult, NotFound>> GetTileAsync(
        int column, int row, HttpContext context, CatalogDbContext db, CancellationToken cancellationToken)
    {
        var tile = await db.MapArts.AsNoTracking()
            .SelectMany(a => a.Tiles)
            .Where(t => t.Column == column && t.Row == row)
            .Select(t => new { t.Image, t.ContentType })
            .FirstOrDefaultAsync(cancellationToken);
        if (tile is null)
        {
            return TypedResults.NotFound();
        }

        context.Response.Headers.CacheControl = TileCaching;
        return TypedResults.File(tile.Image, tile.ContentType);
    }
}
