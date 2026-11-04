namespace SatisfactoryLedger.World.Domain;

/// <summary>A building that runs a recipe: smelter, constructor, assembler, refinery, …</summary>
public sealed class Machine : Buildable
{
    public Machine(BuildableId id, IEnumerable<ItemId> consumes, IEnumerable<ItemId> produces) : base(id)
    {
        Consumes = consumes.ToHashSet();
        Produces = produces.ToHashSet();
    }

    /// <summary>The items its recipe takes in.</summary>
    public IReadOnlySet<ItemId> Consumes { get; }

    /// <summary>The items its recipe makes.</summary>
    public IReadOnlySet<ItemId> Produces { get; }

    public bool ProducesAnyOf(IReadOnlySet<ItemId> items) => Produces.Overlaps(items);
}
