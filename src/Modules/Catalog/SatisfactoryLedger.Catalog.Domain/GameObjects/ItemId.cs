namespace SatisfactoryLedger.Catalog.Domain.GameObjects;

public record ItemId
{
    private ItemId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
    }

    public string Id { get; }

    public static ItemId Of(string id) => new(id);
}