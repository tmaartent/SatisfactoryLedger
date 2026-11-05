using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.World.Domain.Tests;

internal static class Items
{
    public static readonly ItemId IronOre = ItemId.Of("Desc_OreIron_C");
    public static readonly ItemId IronIngot = ItemId.Of("Desc_IronIngot_C");
    public static readonly ItemId IronPlate = ItemId.Of("Desc_IronPlate_C");
    public static readonly ItemId IronRod = ItemId.Of("Desc_IronRod_C");
    public static readonly ItemId Screw = ItemId.Of("Desc_IronScrew_C");
    public static readonly ItemId ReinforcedIronPlate = ItemId.Of("Desc_IronPlateReinforced_C");
}

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

    public TestWorld Machine(string id, ItemId consumes, ItemId produces) => Machine(id, [consumes], [produces]);

    /// <summary>A machine for tracing: only which items matter, so every rate is 1 per minute.</summary>
    public TestWorld Machine(string id, ItemId[] consumes, ItemId[] produces) =>
        Add(new Machine(BuildableId.Of(id), new Recipe(PerMinute(consumes, 1), PerMinute(produces, 1)), ClockSpeed.Percent(100)));

    /// <summary>A machine with one input and one output, at its rates per minute.</summary>
    public TestWorld Machine(string id, ItemId input, decimal asks, ItemId output, decimal makes) =>
        Add(new Machine(BuildableId.Of(id), new Recipe(PerMinute([input], asks), PerMinute([output], makes)), ClockSpeed.Percent(100)));

    /// <param name="capacity">Per minute, or null when it isn't known.</param>
    public TestWorld Belt(string id, decimal? capacity = null) =>
        Add(new Logistics(BuildableId.Of(id), LogisticsKind.Belt, capacity is { } perMinute ? Rate.PerMinute(perMinute) : null));

    public TestWorld Lift(string id) => Add(new Logistics(BuildableId.Of(id), LogisticsKind.Lift));

    public TestWorld Splitter(string id) => Add(new Logistics(BuildableId.Of(id), LogisticsKind.Splitter));

    public TestWorld Merger(string id) => Add(new Logistics(BuildableId.Of(id), LogisticsKind.Merger));

    public TestWorld Container(string id) => Add(new Logistics(BuildableId.Of(id), LogisticsKind.StorageContainer));

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

    private static Dictionary<ItemId, Rate> PerMinute(ItemId[] items, decimal rate) =>
        items.ToDictionary(item => item, _ => Rate.PerMinute(rate));

    private TestWorld Add(Buildable buildable)
    {
        _buildables.Add(buildable);
        return this;
    }
}

internal static class Ids
{
    public static IEnumerable<BuildableId> Of(params string[] ids) => ids.Select(BuildableId.Of);

    /// <summary>Asserts the set of ids regardless of order. A plain Assert.Equal on two sets compares them in order.</summary>
    public static void AssertSet(IEnumerable<BuildableId> actual, params string[] expected) =>
        Assert.Equal(expected.Order(), actual.Select(id => id.Id).Order());
}
