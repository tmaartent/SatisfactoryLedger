using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.World.Domain.Tests;

/// <summary>Builds a small world by hand: buildables by name, then connections in the direction items flow.</summary>
internal sealed class TestWorld
{
    private readonly List<Buildable> _buildables = [];
    private readonly List<Connection> _connections = [];

    /// <summary>A provider whose rate isn't known.</summary>
    public TestWorld Miner(string id) => Add(new Provider(BuildableId.Of(id)));

    public TestWorld Miner(string id, ItemId item, decimal offersPerMinute) =>
        Add(new Provider(BuildableId.Of(id), new ItemOffer(item, Rate.PerMinute(offersPerMinute))));

    public TestWorld Sink(string id) => Add(new Consumer(BuildableId.Of(id)));

    /// <summary>A dimensional depot uploader (BR-A8).</summary>
    public TestWorld Uploader(string id) => Add(new Consumer(BuildableId.Of(id), takesLeftoversOnly: true));

    public TestWorld Machine(string id, ItemId consumes, ItemId produces) => Machine(id, [consumes], [produces]);

    /// <summary>A machine for tracing: only which items matter, so every rate is 1 per minute.</summary>
    public TestWorld Machine(string id, ItemId[] consumes, ItemId[] produces) =>
        Add(new Machine(BuildableId.Of(id), new Recipe(PerMinute(consumes, 1), PerMinute(produces, 1)), ClockSpeed.Percent(100)));

    /// <summary>A machine with one input and one output, at its rates per minute.</summary>
    public TestWorld Machine(string id, ItemId input, decimal asks, ItemId output, decimal makes) =>
        Add(new Machine(BuildableId.Of(id), new Recipe(PerMinute([input], asks), PerMinute([output], makes)), ClockSpeed.Percent(100)));

    /// <summary>A machine with one input and one output that holds <paramref name="holds"/> of its input (BR-A17).</summary>
    public TestWorld MachineHolding(string id, ItemId input, decimal asks, ItemId output, decimal makes, decimal holds) =>
        Add(new Machine(BuildableId.Of(id), new Recipe(PerMinute([input], asks), PerMinute([output], makes)), ClockSpeed.Percent(100))
        {
            InputStock = new Dictionary<ItemId, decimal> { [input] = holds },
        });

    /// <summary>A generator burning <paramref name="burns"/> of <paramref name="fuel"/> a minute, holding <paramref name="holds"/> of it (BR-A17).</summary>
    public TestWorld GeneratorHolding(string id, ItemId fuel, decimal burns, decimal holds) =>
        Add(new Consumer(BuildableId.Of(id), generation: new Generation(fuel, Rate.PerMinute(burns), null, Rate.Zero, null, Rate.Zero))
        {
            InputStock = new Dictionary<ItemId, decimal> { [fuel] = holds },
        });

    /// <param name="capacity">Per minute, or null when it isn't known.</param>
    public TestWorld Belt(string id, decimal? capacity = null) =>
        Add(new Logistics(BuildableId.Of(id), LogisticsKind.Belt, capacity is { } perMinute ? Rate.PerMinute(perMinute) : null));

    public TestWorld Lift(string id) => Add(new Logistics(BuildableId.Of(id), LogisticsKind.Lift));

    public TestWorld Splitter(string id) => Add(new Logistics(BuildableId.Of(id), LogisticsKind.Splitter));

    public TestWorld Merger(string id) => Add(new Logistics(BuildableId.Of(id), LogisticsKind.Merger));

    public TestWorld Container(string id, params (ItemId Item, decimal Count)[] stock) =>
        Add(new Logistics(BuildableId.Of(id), LogisticsKind.StorageContainer, stock: Stock(stock)));

    /// <summary>A train platform or drone port: a provider without a known rate, with what it holds now.</summary>
    public TestWorld Station(string id, params (ItemId Item, decimal Count)[] stock) =>
        Add(new Provider(BuildableId.Of(id), offer: null, Stock(stock)));

    /// <summary>A drone port or truck station that unloads, and burns <paramref name="fuel"/>.</summary>
    public TestWorld StationBurning(string id, ItemId fuel) => Add(new Provider(BuildableId.Of(id), burns: new HashSet<ItemId> { fuel }));

    /// <summary>A truck station that only loads, and burns <paramref name="fuel"/>.</summary>
    /// <summary>A generator burning a known fuel (BR-A8), at the rates given per minute.</summary>
    public TestWorld Generator(string id, ItemId fuel, decimal burns, ItemId? water = null, decimal needs = 0, ItemId? waste = null, decimal leaves = 0) =>
        Add(new Consumer(BuildableId.Of(id), generation: new Generation(
            fuel, Rate.PerMinute(burns), water, Rate.PerMinute(needs), waste, Rate.PerMinute(leaves))));

    /// <summary>A drone port that unloads and burns <paramref name="perMinute"/> of <paramref name="fuel"/> (BR-A16).</summary>
    public TestWorld StationBurning(string id, ItemId fuel, Rate perMinute) =>
        Add(new Provider(BuildableId.Of(id), burns: new HashSet<ItemId> { fuel }, burnRate: perMinute));

    /// <summary>A drone port that only loads and burns <paramref name="perMinute"/> of <paramref name="fuel"/> (BR-A16).</summary>
    public TestWorld LoadingStationBurning(string id, ItemId fuel, Rate perMinute) =>
        Add(new Consumer(BuildableId.Of(id), new HashSet<ItemId> { fuel }, burnRate: perMinute));

    public TestWorld LoadingStationBurning(string id, ItemId fuel) => Add(new Consumer(BuildableId.Of(id), new HashSet<ItemId> { fuel }));

    public TestWorld Junction(string id) => Add(new Logistics(BuildableId.Of(id), LogisticsKind.PipeJunction));

    public TestWorld Pipe(string id) => Add(new Logistics(BuildableId.Of(id), LogisticsKind.Pipe, Rate.PerMinute(600)));

    public TestWorld FluidBuffer(string id) => Add(new Logistics(BuildableId.Of(id), LogisticsKind.FluidBuffer));

    /// <summary>Connects each buildable to the next: <c>Connect("miner", "belt", "smelter")</c>.</summary>
    public TestWorld Connect(params string[] chain)
    {
        for (int i = 1; i < chain.Length; i++)
        {
            _connections.Add(new Connection(BuildableId.Of(chain[i - 1]), BuildableId.Of(chain[i])));
        }

        return this;
    }

    public WorldGraph Build() => new(_buildables, _connections);

    private static Dictionary<ItemId, decimal> Stock((ItemId Item, decimal Count)[] stock) =>
        stock.ToDictionary(entry => entry.Item, entry => entry.Count);

    private static Dictionary<ItemId, Rate> PerMinute(ItemId[] items, decimal rate) =>
        items.ToDictionary(item => item, _ => Rate.PerMinute(rate));

    private TestWorld Add(Buildable buildable)
    {
        _buildables.Add(buildable);
        return this;
    }
}