namespace SatisfactoryLedger.Catalog.Domain.GameObjects;

public sealed record DronePortId
{
    private DronePortId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
    }

    public string Id { get; }

    public static DronePortId Of(string id) => new(id);
}