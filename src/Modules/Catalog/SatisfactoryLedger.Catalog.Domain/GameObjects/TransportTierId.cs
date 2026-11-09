namespace SatisfactoryLedger.Catalog.Domain.GameObjects;

public record TransportTierId
{
    private TransportTierId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
    }

    public string Id { get; }

    public static TransportTierId Of(string id) => new(id);
}