namespace SatisfactoryLedger.World.Domain;

/// <summary>
/// Identifies one buildable in the world. It comes from the save file
/// (<c>Persistent_Level:PersistentLevel.Build_ConstructorMk1_C_2147020985</c>) and stays the same from save to save.
/// </summary>
public sealed record BuildableId
{
    private BuildableId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
    }

    public string Id { get; }

    public static BuildableId Of(string id) => new(id);

    public override string ToString() => Id;
}
