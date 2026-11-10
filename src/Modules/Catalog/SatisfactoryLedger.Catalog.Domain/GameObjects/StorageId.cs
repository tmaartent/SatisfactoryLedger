namespace SatisfactoryLedger.Catalog.Domain.GameObjects;

/// <summary>A building with an inventory, by its class name: <c>Build_StorageContainerMk2_C</c>.</summary>
public sealed record StorageId
{
    private StorageId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
    }

    public string Id { get; }

    public static StorageId Of(string id) => new(id);
}
