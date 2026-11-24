namespace SatisfactoryLedger.Catalog.Domain.GameObjects;

/// <summary>A pipeline pump, by its class name: <c>Build_PipelinePumpMk2_C</c>.</summary>
public sealed record PumpId
{
    private PumpId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
    }

    public string Id { get; }

    public static PumpId Of(string id) => new(id);
}
