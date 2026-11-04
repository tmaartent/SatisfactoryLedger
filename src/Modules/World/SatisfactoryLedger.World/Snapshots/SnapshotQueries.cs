using Microsoft.EntityFrameworkCore;

using SatisfactoryLedger.World.Persistence;

namespace SatisfactoryLedger.World.Snapshots;

/// <summary>Loading a stored snapshot's content. Only these read the (large) content column.</summary>
internal static class SnapshotQueries
{
    /// <summary>A snapshot's world (its session name) and content, or null when there's no such snapshot.</summary>
    public static async Task<(string World, SnapshotDocument Document)?> LoadSnapshotAsync(
        this WorldDbContext db, Guid id, CancellationToken cancellationToken)
    {
        var found = await db.Snapshots.AsNoTracking()
            .Where(snapshot => snapshot.Id == id)
            .Select(snapshot => new { snapshot.SessionName, snapshot.Content })
            .SingleOrDefaultAsync(cancellationToken);

        return found is null ? null : (found.SessionName, SnapshotDocument.FromJson(found.Content));
    }

    /// <summary>The content of the most recently imported snapshot of a world, or null when the world has none.</summary>
    public static async Task<SnapshotDocument?> LoadLatestOfWorldAsync(this WorldDbContext db, string world, CancellationToken cancellationToken)
    {
        string? content = await db.Snapshots.AsNoTracking()
            .Where(snapshot => snapshot.SessionName == world)
            .OrderByDescending(snapshot => snapshot.ImportedAt)
            .Select(snapshot => snapshot.Content)
            .FirstOrDefaultAsync(cancellationToken);

        return content is null ? null : SnapshotDocument.FromJson(content);
    }
}
