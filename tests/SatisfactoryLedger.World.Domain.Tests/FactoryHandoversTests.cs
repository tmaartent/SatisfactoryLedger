using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Domain.Tests.Tracing;
using SatisfactoryLedger.World.Domain.Tracing;

namespace SatisfactoryLedger.World.Domain.Tests;

/// <summary>
/// A factory's handovers (decided 2026-11-11, card 3). The inputs side: per item, what arrives over its inputs, what it
/// needs from outside at 100%, how short it is, and where what arrives comes from. The outputs side: per item, what
/// leaves now and at 100%, where it goes, what its receivers would take, what's sunk, and what can't leave (BR-A18).
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
        (string Id, string Building)[]? buildings = null,
        string[]? usedUp = null)
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
            (usedUp ?? []).Select(BuildableId.Of).ToHashSet(),
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

    // The outputs side. In the worlds below our constructor makes 40 plates a minute at 100%.

    private static OutgoingItem Leaving(FactoryHandovers handovers) => Assert.Single(handovers.Outputs);

    /// <summary>Our constructor's plates go over a belt to <paramref name="end"/>, which <paramref name="world"/> adds.</summary>
    private static WorldGraph PlatesTo(string end, Func<TestWorld, TestWorld> world) => world(new TestWorld()
            .Machine("constructor", Items.IronIngot, 60, Items.IronPlate, 40).Belt("plates"))
        .Connect("constructor", "plates", end).Build();

    private static TestReadings Sending(double plates) => new TestReadings().Into("plates", Items.IronPlate, plates);

    [Fact]
    public void What_leaves_is_what_flows_over_its_outputs_and_at_full_speed_what_its_machines_make_at_100_percent()
    {
        WorldGraph world = PlatesTo("assembler", world => world.Machine("assembler", Items.IronPlate, 30, Items.ReinforcedIronPlate, 5));

        OutgoingItem plates = Leaving(Handovers(world, Sending(20).Blocked("constructor", Items.IronPlate, 0.5), ["constructor"]));

        Assert.Equal((Items.IronPlate, 20.0, 40.0), (plates.Item, plates.Now, plates.AtFullSpeed));
    }

    [Fact]
    public void What_its_own_machines_take_of_what_it_makes_does_not_leave_at_full_speed()
    {
        // Our assembler takes 30 of the 40 plates; the rest goes on to a sink.
        WorldGraph world = new TestWorld()
            .Machine("constructor", Items.IronIngot, 60, Items.IronPlate, 40).Belt("plates").Splitter("split")
            .Belt("to assembler").Machine("assembler", Items.IronPlate, 30, Items.ReinforcedIronPlate, 5)
            .Belt("rest").Sink("sink")
            .Connect("constructor", "plates", "split", "to assembler", "assembler").Connect("split", "rest", "sink").Build();

        OutgoingItem plates = Leaving(Handovers(
            world, new TestReadings().Into("plates", Items.IronPlate, 40).Into("to assembler", Items.IronPlate, 30).Into("rest", Items.IronPlate, 10), ["constructor", "assembler"]));

        Assert.Equal(10, plates.AtFullSpeed, 4);
    }

    [Fact]
    public void What_its_own_machines_take_all_of_does_not_leave()
    {
        WorldGraph world = PlatesTo("assembler", world => world.Machine("assembler", Items.IronPlate, 40, Items.ReinforcedIronPlate, 5));

        Assert.Empty(Handovers(world, Sending(40), ["constructor", "assembler"]).Outputs);
    }

    [Fact]
    public void What_leaves_into_a_container_of_its_own_is_what_flows_into_it()
    {
        WorldGraph world = PlatesTo("box", world => world.Container("box"));

        OutgoingItem plates = Leaving(Handovers(world, Sending(20).Into("box", Items.IronPlate, 20), ["constructor"]));

        Assert.Equal(20, plates.Now, 4);
    }

    private static OutgoingDestination GoesTo(FactoryHandovers handovers) => Assert.Single(Leaving(handovers).Destinations);

    [Fact]
    public void What_reaches_a_machine_of_another_factory_goes_to_that_factory()
    {
        WorldGraph world = PlatesTo("assembler", world => world.Machine("assembler", Items.IronPlate, 30, Items.ReinforcedIronPlate, 5));

        OutgoingDestination destination = GoesTo(Handovers(world, Sending(20), ["constructor"], theirs: ["assembler"]));

        Assert.Equal((OutputDestinationKind.Factory, (Guid?)Theirs, 20.0), (destination.Kind, destination.Owner, destination.PerMinute));
    }

    [Fact]
    public void What_reaches_a_machine_no_factory_has_goes_to_no_factory()
    {
        WorldGraph world = PlatesTo("assembler", world => world.Machine("assembler", Items.IronPlate, 30, Items.ReinforcedIronPlate, 5));

        OutgoingDestination destination = GoesTo(Handovers(world, Sending(20), ["constructor"]));

        Assert.Equal((OutputDestinationKind.NoFactory, (Guid?)null), (destination.Kind, destination.Owner));
    }

    [Fact]
    public void What_reaches_an_awesome_sink_is_sunk()
    {
        WorldGraph world = PlatesTo("sink", world => world.Sink("sink"));

        OutgoingItem plates = Leaving(Handovers(world, Sending(20), ["constructor"], sinks: ["sink"]));

        Assert.Equal((OutputDestinationKind.Sink, 20.0), (Assert.Single(plates.Destinations).Kind, plates.Sunk));
    }

    [Fact]
    public void What_reaches_a_consumer_that_uses_it_up_goes_to_that_consumer_by_its_building()
    {
        WorldGraph world = PlatesTo("portal", world => world.Sink("portal"));

        OutgoingDestination destination = GoesTo(Handovers(world, Sending(20), ["constructor"], buildings: [("portal", "Build_Portal_C")], usedUp: ["portal"]));

        Assert.Equal((OutputDestinationKind.Consumer, "Build_Portal_C"), (destination.Kind, destination.Building));
    }

    [Fact]
    public void What_reaches_any_other_consumer_goes_to_storage_and_is_not_sunk()
    {
        // A station that loads, say: it stores what it gets.
        WorldGraph world = PlatesTo("station", world => world.Sink("station"));

        OutgoingItem plates = Leaving(Handovers(world, Sending(20), ["constructor"]));

        Assert.Equal((OutputDestinationKind.Storage, 0.0), (Assert.Single(plates.Destinations).Kind, plates.Sunk));
    }

    [Fact]
    public void A_belt_that_goes_nowhere_takes_it_nowhere()
    {
        // Shared with another constructor, so the belt isn't ours.
        WorldGraph world = new TestWorld()
            .Machine("constructor", Items.IronIngot, 60, Items.IronPlate, 40).Belt("plates")
            .Machine("their constructor", Items.IronIngot, 60, Items.IronPlate, 40).Belt("theirs")
            .Merger("merge").Belt("loose")
            .Connect("constructor", "plates", "merge", "loose").Connect("their constructor", "theirs", "merge").Build();

        OutgoingDestination destination = GoesTo(Handovers(
            world, Sending(20).Into("theirs", Items.IronPlate, 20).Into("loose", Items.IronPlate, 40), ["constructor"], theirs: ["their constructor"]));

        Assert.Equal(OutputDestinationKind.Nowhere, destination.Kind);
    }

    [Fact]
    public void Where_it_goes_comes_most_first()
    {
        WorldGraph world = new TestWorld()
            .Machine("constructor", Items.IronIngot, 60, Items.IronPlate, 40).Belt("plates").Splitter("split")
            .Belt("to sink").Sink("sink").Belt("to box").Container("box")
            .Connect("constructor", "plates", "split", "to sink", "sink").Connect("split", "to box", "box").Build();

        OutgoingItem plates = Leaving(Handovers(
            world, Sending(30).Into("to sink", Items.IronPlate, 10).Into("to box", Items.IronPlate, 20).Into("box", Items.IronPlate, 20), ["constructor"], sinks: ["sink"]));

        Assert.Equal([OutputDestinationKind.Storage, OutputDestinationKind.Sink], plates.Destinations.Select(destination => destination.Kind));
    }

    [Fact]
    public void Its_receivers_would_take_what_their_recipes_ask_for_at_100_percent()
    {
        WorldGraph world = new TestWorld()
            .Machine("constructor", Items.IronIngot, 60, Items.IronPlate, 40).Belt("plates").Splitter("split").Belt("left").Belt("right")
            .Machine("assembler A", Items.IronPlate, 30, Items.ReinforcedIronPlate, 5)
            .Machine("assembler B", Items.IronPlate, 15, Items.ReinforcedIronPlate, 2.5m)
            .Connect("constructor", "plates", "split", "left", "assembler A").Connect("split", "right", "assembler B").Build();

        OutgoingItem plates = Leaving(Handovers(world, Sending(20).Into("left", Items.IronPlate, 10).Into("right", Items.IronPlate, 10), ["constructor"]));

        Assert.Equal(45, plates.ReceiversNeed, 4);
    }

    [Fact]
    public void A_receiver_that_does_not_use_the_item_asks_for_none_of_it()
    {
        WorldGraph world = PlatesTo("rods", world => world.Machine("rods", Items.IronIngot, 30, Items.IronRod, 15));

        Assert.Equal(0, Leaving(Handovers(world, Sending(20), ["constructor"])).ReceiversNeed);
    }

    [Fact]
    public void A_sink_or_a_container_asks_for_nothing_in_particular()
    {
        WorldGraph world = new TestWorld()
            .Machine("constructor", Items.IronIngot, 60, Items.IronPlate, 40).Belt("plates").Splitter("split")
            .Belt("to sink").Sink("sink").Belt("to box").Container("box")
            .Connect("constructor", "plates", "split", "to sink", "sink").Connect("split", "to box", "box").Build();

        OutgoingItem plates = Leaving(Handovers(
            world, Sending(30).Into("to sink", Items.IronPlate, 10).Into("to box", Items.IronPlate, 20).Into("box", Items.IronPlate, 20), ["constructor"], sinks: ["sink"]));

        Assert.Equal(0, plates.ReceiversNeed);
    }

    [Fact]
    public void A_generator_asks_for_its_fuel_and_its_water()
    {
        WorldGraph world = new TestWorld()
            .Machine("manufacturer", Items.IronPlate, 10, Items.UraniumFuelRod, 0.4m).Belt("rods")
            .Machine("refinery", Items.IronOre, 10, Items.Water, 300).Pipe("water")
            .Generator("plant", Items.UraniumFuelRod, 0.2m, Items.Water, 240, Items.UraniumWaste, 10)
            .Connect("manufacturer", "rods", "plant").Connect("refinery", "water", "plant").Build();

        FactoryHandovers handovers = Handovers(
            world, new TestReadings().Into("rods", Items.UraniumFuelRod, 0.2).Into("water", Items.Water, 240), ["manufacturer", "refinery"]);

        Assert.Equal([(Items.UraniumFuelRod, 0.2), (Items.Water, 240.0)], handovers.Outputs.Select(output => (output.Item, Math.Round(output.ReceiversNeed, 4))));
    }

    [Fact]
    public void A_receiver_is_counted_once_however_many_lines_lead_to_it()
    {
        WorldGraph world = new TestWorld()
            .Machine("constructor", Items.IronIngot, 60, Items.IronPlate, 40).Belt("plates").Splitter("split").Belt("left").Belt("right")
            .Machine("assembler", Items.IronPlate, 30, Items.ReinforcedIronPlate, 5)
            .Connect("constructor", "plates", "split", "left", "assembler").Connect("split", "right", "assembler").Build();

        OutgoingItem plates = Leaving(Handovers(world, Sending(20).Into("left", Items.IronPlate, 10).Into("right", Items.IronPlate, 10), ["constructor"]));

        Assert.Equal(30, plates.ReceiversNeed, 4);
    }

    [Fact]
    public void A_drone_port_asks_for_the_fuel_its_drones_burn()
    {
        WorldGraph world = new TestWorld()
            .Machine("assembler", Items.IronPlate, 10, Items.Battery, 5).Belt("batteries")
            .LoadingStationBurning("drone port", Items.Battery, Rate.PerMinute(2))
            .Connect("assembler", "batteries", "drone port").Build();

        OutgoingItem batteries = Leaving(Handovers(world, new TestReadings().Into("batteries", Items.Battery, 2), ["assembler"]));

        Assert.Equal(2, batteries.ReceiversNeed, 4);
    }

    /// <summary>
    /// Our 16 plates split: 10 to an assembler that asks 30 and runs at 80%, the 6 others to a sink. A constructor of no
    /// factory brings the assembler the other 14 of the 24 it takes.
    /// </summary>
    private static OutgoingItem SharingAnAssembler()
    {
        WorldGraph world = new TestWorld()
            .Machine("constructor", Items.IronIngot, 60, Items.IronPlate, 40).Belt("plates").Splitter("split")
            .Belt("to assembler").Machine("assembler", Items.IronPlate, 30, Items.ReinforcedIronPlate, 5).Belt("rest").Sink("sink")
            .Machine("other constructor", Items.IronIngot, 60, Items.IronPlate, 40).Belt("others")
            .Connect("constructor", "plates", "split", "to assembler", "assembler").Connect("split", "rest", "sink")
            .Connect("other constructor", "others", "assembler").Build();

        return Leaving(Handovers(
            world,
            Sending(16).Into("to assembler", Items.IronPlate, 10).Into("rest", Items.IronPlate, 6).Into("others", Items.IronPlate, 14)
                .Starved("assembler", Items.IronPlate, 0.8),
            ["constructor"],
            sinks: ["sink"]));
    }

    [Fact]
    public void Others_bring_its_receivers_what_they_take_now_less_what_it_brings_them()
    {
        Assert.Equal(14, SharingAnAssembler().OthersBring, 4);
    }

    [Fact]
    public void Its_receivers_are_short_of_what_they_would_take_more_at_100_percent_from_all_sources_together()
    {
        Assert.Equal(6, SharingAnAssembler().Short, 4);
    }

    [Fact]
    public void What_its_output_blocked_machines_would_make_more_at_100_percent_cannot_leave()
    {
        // Blocked at a quarter of its 40 plates.
        WorldGraph world = PlatesTo("box", world => world.Container("box"));

        OutgoingItem plates = Leaving(Handovers(world, Sending(10).Into("box", Items.IronPlate, 10).Blocked("constructor", Items.IronPlate, 0.25), ["constructor"]));

        Assert.Equal(30, plates.CannotLeave, 4);
    }

    [Fact]
    public void What_leaves_reaches_each_end_of_its_lines_per_item()
    {
        WorldGraph world = new TestWorld()
            .Machine("constructor", Items.IronIngot, 60, Items.IronPlate, 40).Belt("plates").Splitter("split")
            .Belt("to sink").Sink("sink").Belt("to box").Container("box")
            .Connect("constructor", "plates", "split", "to sink", "sink").Connect("split", "to box", "box").Build();
        FlowReadings readings = Sending(30).Into("to sink", Items.IronPlate, 10).Into("to box", Items.IronPlate, 20).Into("box", Items.IronPlate, 20).Build(world);

        var ends = FactoryHandovers.Ends(world, readings, new FactoryDefinition([BuildableId.Of("constructor")]).TraceMembers(world, Items.Fluids));

        Assert.Equal(
            [("box", Items.IronPlate, 20.0), ("sink", Items.IronPlate, 10.0)],
            ends.Select(end => (end.End.Id, end.Item, Math.Round(end.PerMinute, 4))).OrderBy(end => end.Id, StringComparer.Ordinal));
    }
}
