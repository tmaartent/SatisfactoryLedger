using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

using SatisfactoryLedger.Catalog.Browsing.Dto.Responses;
using SatisfactoryLedger.Catalog.Domain.Icons;
using SatisfactoryLedger.Catalog.Persistence;

namespace SatisfactoryLedger.Catalog.Browsing;

/// <summary>The game's icons (ADR-0032): which descriptors have one, and each one's image.</summary>
internal static class IconEndpoints
{
    /// <summary>A day, as the map art's tiles: the icons change only with an import, and pages show many.</summary>
    private const string IconCaching = "private, max-age=86400";

    public static RouteGroupBuilder MapIconEndpoints(this RouteGroupBuilder catalog)
    {
        catalog.MapGet("/icons", GetAsync)
            .WithName("GetIcons")
            .WithSummary("The descriptors that have an icon (404 before an import)");

        catalog.MapGet("/icons/{className}", GetIconAsync)
            .WithName("GetIcon")
            .WithSummary("The icon of an item, a resource or a building (by its class: its descriptor's), as the PNG it is");

        return catalog;
    }

    private static async Task<Results<Ok<IconsResponse>, NotFound>> GetAsync(CatalogDbContext db, CancellationToken cancellationToken)
    {
        // Only which there are: the images stay in the database.
        var set = await db.IconSets.AsNoTracking()
            .Select(s => new { s.GameVersion, Descriptors = s.Icons.Select(i => i.ClassName).OrderBy(name => name).ToList() })
            .FirstOrDefaultAsync(cancellationToken);
        return set is null ? TypedResults.NotFound() : TypedResults.Ok(new IconsResponse(set.GameVersion, set.Descriptors));
    }

    private static async Task<Results<FileContentHttpResult, NotFound>> GetIconAsync(
        string className, HttpContext context, CatalogDbContext db, CancellationToken cancellationToken)
    {
        string descriptor = IconSet.DescriptorOf(className);
        var icon = await db.IconSets.AsNoTracking()
            .SelectMany(s => s.Icons)
            .Where(i => i.ClassName == descriptor)
            .Select(i => new { i.Image, i.ContentType })
            .FirstOrDefaultAsync(cancellationToken);
        if (icon is null)
        {
            return TypedResults.NotFound();
        }

        context.Response.Headers.CacheControl = IconCaching;
        return TypedResults.File(icon.Image, icon.ContentType);
    }
}
