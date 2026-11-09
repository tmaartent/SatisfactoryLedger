namespace SatisfactoryLedger.Catalog.Domain.Import;

/// <summary>
/// Something the catalog takes from the game's Docs file: an item, building, transport tier, recipe or extractor.
/// Entries are equal when their ids are equal.
/// </summary>
public interface ICatalogEntry<in TEntry>
{
    /// <summary>False once an import no longer contains the entry. It stays in the catalog, so references to it stay valid.</summary>
    bool IsInGame { get; }

    /// <summary>Takes the data of the same entry from a newer game file, and puts it back in the game.</summary>
    void UpdateFromGame(TEntry fromGame);

    void MarkNoLongerInGame();
}