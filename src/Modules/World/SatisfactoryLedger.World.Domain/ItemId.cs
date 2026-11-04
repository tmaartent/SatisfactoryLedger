namespace SatisfactoryLedger.World.Domain;

/// <summary>An item by the game's class name (<c>Desc_IronPlate_C</c>), the same id the Catalog uses.</summary>
public sealed record ItemId
{
    private ItemId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
    }

    public string Id { get; }

    public static ItemId Of(string id) => new(id);

    public override string ToString() => Id;
}
