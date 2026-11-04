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

    public TestWorld Miner(string id) => Add(new Provider(BuildableId.Of(id)));

    public TestWorld Sink(string id) => Add(new Consumer(BuildableId.Of(id)));

    public TestWorld Machine(string id, ItemId consumes, ItemId produces) => Machine(id, [consumes], [produces]);

    public TestWorld Machine(string id, ItemId[] consumes, ItemId[] produces) =>
        Add(new Machine(BuildableId.Of(id), consumes, produces));

    public TestWorld Belt(string id) => Add(new Logistics(BuildableId.Of(id), LogisticsKind.Belt));

    public TestWorld Lift(string id) => Add(new Logistics(BuildableId.Of(id), LogisticsKind.Lift));

    public TestWorld Splitter(string id) => Add(new Logistics(BuildableId.Of(id), LogisticsKind.Splitter));

    public TestWorld Merger(string id) => Add(new Logistics(BuildableId.Of(id), LogisticsKind.Merger));

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
