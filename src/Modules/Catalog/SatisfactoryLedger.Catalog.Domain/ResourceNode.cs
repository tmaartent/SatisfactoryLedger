namespace SatisfactoryLedger.Catalog.Domain;

/// <summary>
/// A spot on the map where a resource can be extracted (ADR-0015). Game data like the rest of the Catalog, but it comes
/// from the node table the map tool writes, not from the Docs file.
/// </summary>
public class ResourceNode : ICatalogEntry<ResourceNode>
{
    private ResourceNode(ResourceNodeId id, ResourceNodeKind kind, ItemId resource, Purity purity)
    {
        Id = id;
        Kind = kind;
        Resource = resource;
        Purity = purity;
    }

    public static ResourceNode Create(ResourceNodeId id, ResourceNodeKind kind, ItemId resource, Purity purity, MapPosition position)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(position);

        return new ResourceNode(id, kind, resource, purity) { Position = position };
    }

    public ResourceNodeId Id { get; }

    public ResourceNodeKind Kind { get; private set; }

    /// <summary>The item extracted here.</summary>
    public ItemId Resource { get; private set; }

    public Purity Purity { get; private set; }

    // EF Core binds only scalar constructor parameters; it sets this complex value afterwards.
    public MapPosition Position { get; private set; } = null!;

    public bool IsInGame { get; private set; } = true;

    public void UpdateFromGame(ResourceNode fromGame)
    {
        if (!Equals(fromGame))
        {
            throw new ArgumentException($"{fromGame} is a different resource node than {this}", nameof(fromGame));
        }

        Kind = fromGame.Kind;
        Resource = fromGame.Resource;
        Purity = fromGame.Purity;
        Position = fromGame.Position;
        IsInGame = true;
    }

    public void MarkNoLongerInGame()
    {
        IsInGame = false;
    }

    public override bool Equals(object? obj) => obj is ResourceNode other && Id.Equals(other.Id);

    public override int GetHashCode() => Id.GetHashCode();

    public override string ToString() => Id.Id;
}