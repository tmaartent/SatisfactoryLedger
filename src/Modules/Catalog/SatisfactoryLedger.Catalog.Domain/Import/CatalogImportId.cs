namespace SatisfactoryLedger.Catalog.Domain.Import;

public sealed record CatalogImportId
{
    private CatalogImportId(Guid id)
    {
        Id = id;
    }

    public Guid Id { get; }

    /// <summary>A version 7 GUID: it starts with a timestamp, so new ids sort after old ones.</summary>
    public static CatalogImportId New() => new(Guid.CreateVersion7());

    public static CatalogImportId Of(Guid id) => new(id);
}