namespace SatisfactoryLedger.World.Domain;

/// <summary>
/// Anything built in the world that items move through. The subclasses are the roles that matter for the analysis:
/// <see cref="Machine"/>, <see cref="Provider"/>, <see cref="Consumer"/> and <see cref="Logistics"/>.
/// </summary>
public abstract class Buildable
{
    private protected Buildable(BuildableId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        Id = id;
    }

    public BuildableId Id { get; }

    public override string ToString() => $"{GetType().Name} {Id}";
}
