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
    private static readonly IReadOnlyDictionary<ItemId, decimal> NoStock = new Dictionary<ItemId, decimal>();

    private IReadOnlySet<ItemId>? _produces;

    private protected Buildable(BuildableId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        Id = id;
    }

    public BuildableId Id { get; }

    /// <summary>
    /// What it holds of its inputs, per item (fluids in m³), at save time: a machine its ingredients, a generator its
    /// fuel; empty for the others. A stock that lasts counts as supplied (BR-A17).
    /// </summary>
    public IReadOnlyDictionary<ItemId, decimal> InputStock { get; init; } = NoStock;

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

    /// <summary>What it asks of <paramref name="item"/> per minute at 100% (see <see cref="Demand"/>); zero when none is known.</summary>
    public Rate DemandOf(ItemId item) => Demand.GetValueOrDefault(item) ?? Rate.Zero;

    /// <summary>What it makes of <paramref name="item"/> per minute at 100% (see <see cref="Production"/>); zero when none.</summary>
    public Rate ProductionOf(ItemId item) => Production.GetValueOrDefault(item) ?? Rate.Zero;

    /// <summary>The items it makes.</summary>
    public IReadOnlySet<ItemId> Produces => _produces ??= Production.Keys.ToHashSet();

    /// <summary>Whether it makes <paramref name="item"/>: a machine its recipe's products, a generator its waste when some leaves it.</summary>
    public bool Makes(ItemId item) => Produces.Contains(item);

    /// <summary>
    /// Whether it takes <paramref name="item"/> at the end of a line: what it <see cref="Consumes"/>, so a machine its
    /// recipe's inputs and a generator burning a known fuel its fuel and water.
    /// </summary>
    public virtual bool Takes(ItemId item) => Consumes.Contains(item);

    public override string ToString() => $"{GetType().Name} {Id}";

    /// <summary>BR-A16: a drone port burns the fuel in its slot at its drones' rate, when that's known.</summary>
    private protected static IReadOnlyDictionary<ItemId, Rate> Burning(IReadOnlySet<ItemId> burns, FuelBurn? burn) =>
        burn is FuelBurn.Known known && burns.Count == 1 ? new Dictionary<ItemId, Rate> { [burns.Single()] = known.PerMinute } : NoRates;

    /// <summary>The fuel in its slot it takes in: none at a drone port where no drone refuels (issue #44).</summary>
    private protected static IReadOnlySet<ItemId> Fuelled(IReadOnlySet<ItemId> burns, FuelBurn? burn) =>
        burn is FuelBurn.NoDroneRefuels ? NoItems : burns;
}
