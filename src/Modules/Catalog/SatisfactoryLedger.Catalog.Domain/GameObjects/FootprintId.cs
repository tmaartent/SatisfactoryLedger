namespace SatisfactoryLedger.Catalog.Domain.GameObjects;

/// <summary>A building's class name, e.g. <c>Build_ConstructorMk1_C</c>: what its footprint belongs to.</summary>
public sealed record FootprintId
{
    private FootprintId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
    }

    public string Id { get; }

    public static FootprintId Of(string id) => new(id);
}