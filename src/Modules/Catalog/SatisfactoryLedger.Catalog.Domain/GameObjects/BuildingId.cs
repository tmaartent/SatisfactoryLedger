namespace SatisfactoryLedger.Catalog.Domain.GameObjects;

public record BuildingId
{
    public string Id { get; }

    private BuildingId(string id)
    {
        this.Id = id;
    }
    public static BuildingId Of(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        
        return new BuildingId(id);
    }
}