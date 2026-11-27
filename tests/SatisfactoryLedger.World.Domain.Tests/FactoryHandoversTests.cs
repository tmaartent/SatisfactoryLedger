using SatisfactoryLedger.World.Domain.Tests.Tracing;
using SatisfactoryLedger.World.Domain.Tracing;

namespace SatisfactoryLedger.World.Domain.Tests;

/// <summary>
/// A factory's handovers, the inputs side (decided 2026-11-11, card 3): per item, what arrives over its inputs, what it
/// needs from outside at 100%, how short it is, and where what arrives comes from.
/// </summary>
public class FactoryHandoversTests
{
    private static readonly Guid Ours = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Theirs = Guid.Parse("00000000-0000-0000-0000-0000000000b2");

    /// <summary>
    /// The handovers of the factory of <paramref name="machines"/>: they're ours, the machines in
    /// <paramref name="theirs"/> another factory's, and every other machine no factory's.
    /// </summary>
    private static FactoryHandovers Handovers(
        WorldGraph world,
        TestReadings readings,
        string[] machines,
        string[]? theirs = null,
        string[]? sinks = null,
        (string Id, string Building)[]? buildings = null)
    {
        FactoryMembers members = new FactoryDefinition(machines.Select(BuildableId.Of)).TraceMembers(world, Items.Fluids);
        Dictionary<BuildableId, Guid> owners = members.Machines.ToDictionary(id => id, _ => Ours);
        foreach (string id in theirs ?? [])
        {
            owners[BuildableId.Of(id)] = Theirs;
        }

        return FactoryHandovers.Of(
            world,
            readings.Build(world),
            members,
            owners,
            (sinks ?? []).Select(BuildableId.Of).ToHashSet(),
            (buildings ?? []).ToDictionary(building => BuildableId.Of(building.Id), building => building.Building));
    }

    private static IncomingItem Only(FactoryHandovers handovers) => Assert.Single(handovers.Inputs);

    [Fact]
    public void What_arrives_is_what_flows_over_its_input_and_what_it_needs_is_what_its_machines_take_at_full_speed()
    {
        WorldGraph world = new TestWorld()
            .Miner("miner").Belt("ore", 120).Machine("smelter", Items.IronOre, 60, Items.IronIngot, 60)
            .Connect("miner", "ore", "smelter").Build();

        IncomingItem ore = Only(Handovers(world, new TestReadings().Starved("smelter", Items.IronOre, 0.5).Into("ore", Items.IronOre, 30), ["smelter"]));

        Assert.Equal((Items.IronOre, 30.0, 60.0), (ore.Item, ore.Arriving, ore.Need));
    }

    /// <summary>
    /// smelter A (another factory's) and smelter B (no factory's) each send 15 ingots onto belts that merge onto the one
    /// into our constructor.
    /// </summary>
    private static WorldGraph Merging() => new TestWorld()
        .Machine("smelter A", Items.IronOre, 30, Items.IronIngot, 30).Belt("from A")
        .Machine("smelter B", Items.IronOre, 30, Items.IronIngot, 30).Belt("from B")
        .Merger("merge").Belt("ingots").Machine("constructor", Items.IronIngot, 60, Items.IronPlate, 40)
        .Connect("smelter A", "from A", "merge", "ingots", "constructor").Connect("smelter B", "from B", "merge").Build();

    private static TestReadings MergingReadings() => new TestReadings()
        .Starved("smelter A", Items.IronOre, 0.5).Starved("smelter B", Items.IronOre, 0.5).Starved("constructor", Items.IronIngot, 0.5)
        .Into("from A", Items.IronIngot, 15).Into("from B", Items.IronIngot, 15).Into("ingots", Items.IronIngot, 30);

    [Fact]
    public void Machines_of_another_factory_are_a_source_of_that_factory_with_the_machines_it_starts_at()
    {
        IncomingSource source = Only(Handovers(Merging(), MergingReadings(), ["constructor"], theirs: ["smelter A"])).Sources
            .Single(source => source.Kind == InputSourceKind.Factory);

        Assert.Equal((Theirs, 15.0), (source.Owner, source.PerMinute));
        Assert.Equal([BuildableId.Of("smelter A")], source.Machines);
    }

    [Fact]
    public void Machines_of_no_factory_are_one_source_of_no_factory()
    {
        IncomingSource source = Only(Handovers(Merging(), MergingReadings(), ["constructor"])).Sources.Single();

        Assert.Equal((InputSourceKind.NoFactory, (Guid?)null, 30.0), (source.Kind, source.Owner, source.PerMinute));
        Assert.Equal([BuildableId.Of("smelter A"), BuildableId.Of("smelter B")], source.Machines);
    }

    [Fact]
    public void Sources_come_most_first()
    {
        IncomingItem ingots = Only(Handovers(
            Merging(),
            MergingReadings().Into("from A", Items.IronIngot, 10).Into("from B", Items.IronIngot, 20),
            ["constructor"],
            theirs: ["smelter A"]));

        Assert.Equal([InputSourceKind.NoFactory, InputSourceKind.Factory], ingots.Sources.Select(source => source.Kind));
    }

    [Fact]
    public void A_building_is_a_source_on_its_own_by_its_class()
    {
        WorldGraph world = new TestWorld()
            .Miner("miner").Belt("ore", 120).Machine("smelter", Items.IronOre, 60, Items.IronIngot, 60)
            .Connect("miner", "ore", "smelter").Build();

        IncomingSource source = Only(Handovers(
            world, new TestReadings().Starved("smelter", Items.IronOre, 0.5).Into("ore", Items.IronOre, 30), ["smelter"], buildings: [("miner", "Build_MinerMk1_C")]))
            .Sources.Single();

        Assert.Equal((InputSourceKind.Building, "Build_MinerMk1_C", BuildableId.Of("miner"), 30.0), (source.Kind, source.Building, source.Id, source.PerMinute));
        Assert.Empty(source.Machines);
    }

    [Fact]
    public void A_belt_that_starts_at_nothing_brings_it_from_nowhere()
    {
        // A loose belt shared with another smelter, so it isn't ours.
        WorldGraph world = new TestWorld()
            .Belt("loose", 120).Splitter("split").Belt("to us", 120).Belt("to them", 120)
            .Machine("smelter", Items.IronOre, 60, Items.IronIngot, 60).Machine("their smelter", Items.IronOre, 60, Items.IronIngot, 60)
            .Connect("loose", "split", "to us", "smelter").Connect("split", "to them", "their smelter").Build();

        IncomingSource source = Only(Handovers(
            world,
            new TestReadings().Starved("smelter", Items.IronOre, 0.5).Into("loose", Items.IronOre, 60).Into("to us", Items.IronOre, 30).Into("to them", Items.IronOre, 30),
            ["smelter"]))
            .Sources.Single();

        Assert.Equal((InputSourceKind.Nowhere, 30.0), (source.Kind, source.PerMinute));
    }

    // Short: what it would need more at 100% than arrives now and its sources could bring more (decided 2026-11-11).
    // In the worlds below our smelter or constructor asks 100 a minute and 60 arrive, unless said otherwise.

    [Fact]
    public void What_its_sources_have_to_spare_can_come_when_the_line_has_room()
    {
        WorldGraph world = new TestWorld()
            .Miner("miner", Items.IronOre, 120).Belt("ore", 120).Machine("smelter", Items.IronOre, 100, Items.IronIngot, 100)
            .Connect("miner", "ore", "smelter").Build();

        IncomingItem ore = Only(Handovers(world, new TestReadings().Spare("miner", Items.IronOre, 30).Into("ore", Items.IronOre, 60), ["smelter"]));

        Assert.Equal(10, ore.Short(), 4);
    }

    [Fact]
    public void A_full_belt_on_the_way_lets_nothing_more_through()
    {
        WorldGraph world = new TestWorld()
            .Miner("miner", Items.IronOre, 120).Belt("full", 60).Belt("ore", 120).Machine("smelter", Items.IronOre, 100, Items.IronIngot, 100)
            .Connect("miner", "full", "ore", "smelter").Build();

        IncomingItem ore = Only(Handovers(
            world, new TestReadings().Spare("miner", Items.IronOre, 60).Into("full", Items.IronOre, 60).Into("ore", Items.IronOre, 60), ["smelter"]));

        Assert.Equal(40, ore.Short(), 4);
    }

    [Fact]
    public void The_tightest_room_on_the_way_decides()
    {
        // The miner has 60 to spare; the belt has room for 20 more.
        WorldGraph world = new TestWorld()
            .Miner("miner", Items.IronOre, 120).Belt("ore", 80).Machine("smelter", Items.IronOre, 100, Items.IronIngot, 100)
            .Connect("miner", "ore", "smelter").Build();

        IncomingItem ore = Only(Handovers(world, new TestReadings().Spare("miner", Items.IronOre, 60).Into("ore", Items.IronOre, 60), ["smelter"]));

        Assert.Equal(20, ore.Short(), 4);
    }

    [Fact]
    public void Sources_sharing_a_narrow_belt_share_its_room()
    {
        // Two smelters of another factory, each with 30 to make more, merge onto a belt with room for 30 more.
        WorldGraph world = new TestWorld()
            .Machine("smelter A", Items.IronOre, 60, Items.IronIngot, 60).Belt("a", 120)
            .Machine("smelter B", Items.IronOre, 60, Items.IronIngot, 60).Belt("b", 120)
            .Merger("merge").Belt("ingots", 90).Machine("constructor", Items.IronIngot, 100, Items.IronPlate, 50)
            .Connect("smelter A", "a", "merge", "ingots", "constructor").Connect("smelter B", "b", "merge").Build();

        IncomingItem ingots = Only(Handovers(
            world,
            new TestReadings().Starved("smelter A", Items.IronOre, 0.5).Starved("smelter B", Items.IronOre, 0.5)
                .Into("a", Items.IronIngot, 30).Into("b", Items.IronIngot, 30).Into("ingots", Items.IronIngot, 60),
            ["constructor"],
            theirs: ["smelter A", "smelter B"]));

        Assert.Equal(10, ingots.Short(), 4);
    }

    [Fact]
    public void Each_of_its_inputs_takes_what_its_own_line_can_bring()
    {
        // A splitter sends another factory's ingots to two of our constructors, each line with room for 20 more; the
        // smelter could make 60 more. Both ask 100, so 200 in all, and 60 arrive.
        WorldGraph world = new TestWorld()
            .Machine("smelter", Items.IronOre, 120, Items.IronIngot, 120).Belt("ingots", 400).Splitter("split").Belt("left", 50).Belt("right", 50)
            .Machine("constructor L", Items.IronIngot, 100, Items.IronPlate, 50).Machine("constructor R", Items.IronIngot, 100, Items.IronPlate, 50)
            .Connect("smelter", "ingots", "split", "left", "constructor L").Connect("split", "right", "constructor R").Build();

        IncomingItem ingots = Only(Handovers(
            world,
            new TestReadings().Starved("smelter", Items.IronOre, 0.5)
                .Into("ingots", Items.IronIngot, 60).Into("left", Items.IronIngot, 30).Into("right", Items.IronIngot, 30),
            ["constructor L", "constructor R"],
            theirs: ["smelter"]));

        Assert.Equal(100, ingots.Short(), 4);
    }

    /// <summary>
    /// Another factory's smelter runs flat out onto a full line; a splitter sends 60 to us and 30 over the overflow to
    /// <paramref name="end"/>.
    /// </summary>
    private static WorldGraph Overflowing(decimal roomToUs, Func<TestWorld, TestWorld> end) =>
        end(new TestWorld()
            .Machine("smelter", Items.IronOre, 90, Items.IronIngot, 90).Belt("line", 90).Splitter("split")
            .Belt("to us", 60 + roomToUs).Belt("overflow", 100).Machine("constructor", Items.IronIngot, 100, Items.IronPlate, 50))
            .Connect("smelter", "line", "split", "to us", "constructor").Connect("split", "overflow", "end").Build();

    private static TestReadings OverflowingReadings() => new TestReadings()
        .Running("smelter").Into("line", Items.IronIngot, 90).Into("to us", Items.IronIngot, 60).Into("overflow", Items.IronIngot, 30);

    [Fact]
    public void What_overflows_into_a_sink_could_come_instead()
    {
        WorldGraph world = Overflowing(roomToUs: 100, world => world.Sink("end"));

        IncomingItem ingots = Only(Handovers(world, OverflowingReadings(), ["constructor"], theirs: ["smelter"], sinks: ["end"]));

        Assert.Equal(10, ingots.Short(), 4);
    }

    [Fact]
    public void What_overflows_into_a_sink_could_come_only_as_far_as_its_line_has_room()
    {
        WorldGraph world = Overflowing(roomToUs: 10, world => world.Sink("end"));

        IncomingItem ingots = Only(Handovers(world, OverflowingReadings(), ["constructor"], theirs: ["smelter"], sinks: ["end"]));

        Assert.Equal(30, ingots.Short(), 4);
    }

    [Fact]
    public void What_another_machine_takes_is_not_there_to_take()
    {
        WorldGraph world = Overflowing(roomToUs: 100, world => world.Machine("end", Items.IronIngot, 30, Items.IronRod, 30));

        IncomingItem ingots = Only(Handovers(world, OverflowingReadings().Running("end"), ["constructor"], theirs: ["smelter"]));

        Assert.Equal(40, ingots.Short(), 4);
    }

    /// <summary>A station unloading 30 ore a minute (BR-A9) onto a belt to our smelter, <paramref name="via"/> what's between.</summary>
    private static IncomingItem FromStation(decimal stock, Func<TestWorld, TestWorld> world, Func<TestReadings, TestReadings> readings, params string[] via)
    {
        WorldGraph built = world(new TestWorld()
                .Station("station", (Items.IronOre, stock)).Belt("ore", 600).Machine("smelter", Items.IronOre, 100, Items.IronIngot, 100))
            .Connect(["station", .. via, "ore", "smelter"]).Build();

        return Only(Handovers(built, readings(new TestReadings().Unknown("station", Items.IronOre, 30).Into("ore", Items.IronOre, 30)), ["smelter"]));
    }

    [Fact]
    public void A_station_could_bring_more_as_far_as_its_stock_lasts_half_an_hour()
    {
        // 1,200 ore last 30 minutes at 40 a minute: 10 more than the 30 it brings.
        IncomingItem ore = FromStation(1200, world => world, readings => readings);

        Assert.Equal(60, ore.Short(), 4);
    }

    [Fact]
    public void The_stock_of_the_containers_a_station_unloads_into_counts_with_its_own()
    {
        // 2,400 ore last 30 minutes at 80 a minute: 50 more than the 30 it brings.
        IncomingItem ore = FromStation(
            1200,
            world => world.Belt("unloading", 600).Container("buffer", (Items.IronOre, 1200)),
            readings => readings.Into("unloading", Items.IronOre, 30).Into("buffer", Items.IronOre, 30),
            "unloading",
            "buffer");

        Assert.Equal(20, ore.Short(), 4);
    }

    [Fact]
    public void Its_own_machines_are_no_outside_supply()
    {
        // Our smelter A and smelter B of no factory each make 15 of their 30 ingots; our constructor asks 90, less the 30
        // smelter A makes. Only smelter B's 15 could come more.
        WorldGraph world = new TestWorld()
            .Machine("smelter A", Items.IronOre, 30, Items.IronIngot, 30).Belt("from A")
            .Machine("smelter B", Items.IronOre, 30, Items.IronIngot, 30).Belt("from B")
            .Merger("merge").Belt("ingots").Machine("constructor", Items.IronIngot, 90, Items.IronPlate, 60)
            .Connect("smelter A", "from A", "merge", "ingots", "constructor").Connect("smelter B", "from B", "merge").Build();

        IncomingItem ingots = Only(Handovers(world, MergingReadings(), ["constructor", "smelter A"]));

        Assert.Equal(15, ingots.Short(), 4);
    }

    [Fact]
    public void A_shortfall_within_a_hundredth_of_a_percent_of_the_need_is_none()
    {
        WorldGraph world = new TestWorld()
            .Miner("miner").Belt("ore", 120).Machine("smelter", Items.IronOre, 100, Items.IronIngot, 100)
            .Connect("miner", "ore", "smelter").Build();

        IncomingItem ore = Only(Handovers(world, new TestReadings().Into("ore", Items.IronOre, 99.995), ["smelter"]));

        Assert.Equal(0, ore.Short());
    }
}
