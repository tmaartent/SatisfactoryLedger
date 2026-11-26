using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.World.Domain;

/// <summary>
/// Brings items into the network from outside: a miner, an extractor, or a train, truck or drone station that unloads.
/// Never part of a factory (BR-W1).
/// </summary>
/// <param name="offer">
/// What it offers per minute (BR-A2), or null when that isn't known, such as an extractor on a node the Catalog
/// doesn't know, or a station. The analysis then assumes it supplies what is asked (BR-A9).
/// </param>
/// <param name="stock">What it holds now, per item: what's on a platform, or what a drone port received.</param>
/// <param name="burns">The fuel a drone port or truck station burns.</param>
/// <param name="fuelBurn">For a drone port: what it burns of its fuel (BR-A16); not known when left out.</param>
public sealed class Provider(
    BuildableId id,
    ItemOffer? offer = null,
    IReadOnlyDictionary<ItemId, decimal>? stock = null,
    IReadOnlySet<ItemId>? burns = null,
    FuelBurn? fuelBurn = null) : Buildable(id)
{
    public ItemOffer? Offer { get; } = offer;

    /// <summary>
    /// The fuel in its slot: fuel coming in takes only what the line's other consumers leave (step 14), unless its
    /// <see cref="FuelBurn"/> is known, or none of it where no drone refuels.
    /// </summary>
    public IReadOnlySet<ItemId> Burns { get; } = burns ?? new HashSet<ItemId>();

    /// <summary>What it burns of its fuel (BR-A16).</summary>
    public FuelBurn FuelBurn { get; } = fuelBurn ?? new FuelBurn.NotKnown();

    /// <summary>What it holds now, per item. Empty when it holds nothing or that isn't known. Decides confirmed supplied (BR-A9).</summary>
    public IReadOnlyDictionary<ItemId, decimal> Stock { get; } = stock ?? new Dictionary<ItemId, decimal>();

    /// <summary>
    /// Its inventory, when the Catalog knows its size: at the end of a line it takes an item only while that fits
    /// (BR-A7). Null when its size isn't known; it then takes everything, as before.
    /// </summary>
    public Inventory? Inventory { get; init; }

    /// <summary>
    /// A station that unloads, not an extractor: when it holds nothing, it's taken to carry what the stations its
    /// lines meet hold (BR-A9).
    /// </summary>
    public bool IsStation { get; init; }

    /// <summary>A drone port the fuel its drones burn (BR-A16); nothing for the others.</summary>
    public override IReadOnlyDictionary<ItemId, Rate> Demand { get; } = Burning(burns ?? NoItems, fuelBurn);

    /// <summary>What it brings in is its <see cref="Offer"/>, not something it makes.</summary>
    public override IReadOnlyDictionary<ItemId, Rate> Production => NoRates;

    /// <summary>A drone port or truck station its fuel, whether or not its rate is known; none where no drone refuels.</summary>
    public override IReadOnlySet<ItemId> Consumes { get; } = Fuelled(burns ?? NoItems, fuelBurn);
}