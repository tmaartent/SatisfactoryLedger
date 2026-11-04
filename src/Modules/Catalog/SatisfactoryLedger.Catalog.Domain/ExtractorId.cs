namespace SatisfactoryLedger.Catalog.Domain;

public sealed record ExtractorId
{
    private ExtractorId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
    }

    public string Id { get; }

    public static ExtractorId Of(string id) => new(id);
}
