using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.World.Domain;

/// <summary>
/// Anything built in the world that items move through. The subclasses are the roles that matter for the analysis:
/// <see cref="Machine"/>, <see cref="Provider"/>, <see cref="Consumer"/> and <see cref="Logistics"/>.
/// </summary>
public abstract class Buildable
{
    private protected static readonly IReadOnlyDictionary<ItemId, Rate> NoRates = new Dictionary<ItemId, Rate>();
    private protected static readonly IReadOnlySet<ItemId> NoItems = new HashSet<ItemId>();

    private IReadOnlySet<ItemId>? _produces;

    private protected Buildable(BuildableId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        Id = id;
    }

    public BuildableId Id { get; }

    /// <summary>
    /// Per input item, what it asks per minute at 100%, when that's known: a machine its recipe (BR-A1), a generator
    /// burning a known fuel its fuel and water (BR-A8), a drone port its drones' fuel (BR-A16). Empty both when it takes
    /// nothing and when what it takes has no known rate: a sink, a truck station, a generator whose fuel isn't known.
    /// </summary>
    public abstract IReadOnlyDictionary<ItemId, Rate> Demand { get; }

    /// <summary>Per output item, what it makes per minute at 100%: a machine its recipe, a generator its waste.</summary>
    public abstract IReadOnlyDictionary<ItemId, Rate> Production { get; }

    /// <summary>The items it takes in, whether or not their rate is known: a truck station its fuel.</summary>
    public abstract IReadOnlySet<ItemId> Consumes { get; }

    /// <summary>The items it makes.</summary>
    public IReadOnlySet<ItemId> Produces => _produces ??= Production.Keys.ToHashSet();

    public override string ToString() => $"{GetType().Name} {Id}";

    /// <summary>BR-A16: a drone port burns the fuel in its slot at its drones' rate, when that's known.</summary>
    private protected static IReadOnlyDictionary<ItemId, Rate> Burning(IReadOnlySet<ItemId> burns, Rate? burnRate) =>
        burnRate is { } rate && burns.Count == 1 ? new Dictionary<ItemId, Rate> { [burns.Single()] = rate } : NoRates;
}
