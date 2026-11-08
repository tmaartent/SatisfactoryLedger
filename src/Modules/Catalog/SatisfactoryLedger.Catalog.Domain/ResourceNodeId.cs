namespace SatisfactoryLedger.Catalog.Domain;

/// <summary>A node by the name the game and the save use: <c>BP_ResourceNode35</c>.</summary>
public sealed record ResourceNodeId
{
    private ResourceNodeId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
    }

    public string Id { get; }

    public static ResourceNodeId Of(string id) => new(id);
}