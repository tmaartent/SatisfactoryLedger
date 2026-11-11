namespace SatisfactoryLedger.Catalog.Domain.GameObjects;

/// <summary>Anything the player places, by its class name: <c>Build_TrainDockingStation_C</c>.</summary>
public sealed record BuildableId
{
    private BuildableId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
    }

    public string Id { get; }

    public static BuildableId Of(string id) => new(id);
}
