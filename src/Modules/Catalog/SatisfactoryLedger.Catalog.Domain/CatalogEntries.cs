namespace SatisfactoryLedger.Catalog.Domain;

public static class CatalogEntries
{
    /// <summary>
    /// Brings the stored entries in line with a game file (ADR-0005): entries only in the file are added,
    /// stored entries in the file are updated from it, and stored entries missing from the file are
    /// marked no longer in the game instead of being deleted.
    /// </summary>
    public static CatalogEntryChanges<TEntry> Reconcile<TEntry>(IEnumerable<TEntry> stored, IEnumerable<TEntry> fromGame)
        where TEntry : ICatalogEntry<TEntry>
    {
        var inGameFile = new HashSet<TEntry>();
        foreach (TEntry entry in fromGame)
        {
            if (!inGameFile.Add(entry))
            {
                throw new ArgumentException($"The game file contains {entry} more than once", nameof(fromGame));
            }
        }

        var storedById = stored.ToDictionary(entry => entry);
        List<TEntry> added = [];
        List<TEntry> updated = [];
        List<TEntry> noLongerInGame = [];

        foreach (TEntry entry in inGameFile)
        {
            if (storedById.TryGetValue(entry, out TEntry? storedEntry))
            {
                storedEntry.UpdateFromGame(entry);
                updated.Add(storedEntry);
            }
            else
            {
                added.Add(entry);
            }
        }

        foreach (TEntry storedEntry in storedById.Keys.Where(entry => entry.IsInGame && !inGameFile.Contains(entry)))
        {
            storedEntry.MarkNoLongerInGame();
            noLongerInGame.Add(storedEntry);
        }

        return new CatalogEntryChanges<TEntry>(added, updated, noLongerInGame);
    }
}