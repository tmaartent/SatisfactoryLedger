namespace SatisfactoryLedger.World.Domain.Tests;

/// <summary>A factory defined by its machines: the logistics it owns, its outputs and its inputs (BR-W2 to BR-W5).</summary>
public class FactoryDefinitionTests
{
    private static BuildableId Id(string name) => BuildableId.Of(name);

    private static FactoryMembers Trace(TestWorld world, params string[] machines) =>
        new FactoryDefinition(machines.Select(Id)).TraceMembers(world.Build());

    private static IEnumerable<string> Names(IEnumerable<BuildableId> ids) => ids.Select(id => id.Id).Order(StringComparer.Ordinal);

    /// <summary>miner → ore → smelter → ingots → constructor → plates → box (a storage container at the end).</summary>
    private static TestWorld PlateLine() => new TestWorld()
        .Miner("miner").Belt("ore").Machine("smelter", Items.IronOre, Items.IronIngot)
        .Belt("ingots").Machine("constructor", Items.IronIngot, Items.IronPlate).Belt("plates").Container("box")
        .Connect("miner", "ore", "smelter", "ingots", "constructor", "plates", "box");

    [Fact]
    public void A_factory_is_the_machines_the_player_selected()
    {
        Assert.Equal(["constructor", "smelter"], Names(Trace(PlateLine(), "smelter", "constructor").Machines));
    }

    [Fact]
    public void The_belts_between_its_machines_belong_to_it()
    {
        Assert.Contains(Id("ingots"), Trace(PlateLine(), "smelter", "constructor").Logistics);
    }

    [Fact]
    public void Its_line_to_its_own_storage_container_belongs_to_it_and_the_container_is_an_output()
    {
        FactoryMembers factory = Trace(PlateLine(), "smelter", "constructor");

        Assert.Superset(new HashSet<BuildableId> { Id("plates"), Id("box") }, factory.Logistics.ToHashSet());
        FactoryConnection output = Assert.Single(factory.Outputs);
        Assert.Equal((Id("box"), (BuildableId?)null), (output.Inside, output.Outside));
        Assert.Equal([Items.IronPlate], output.Items);
    }

    [Fact]
    public void A_supply_line_from_a_miner_that_feeds_only_this_factory_belongs_to_it_and_the_miner_is_its_supplier()
    {
        FactoryMembers factory = Trace(PlateLine(), "smelter", "constructor");

        Assert.Contains(Id("ore"), factory.Logistics);
        FactoryConnection input = Assert.Single(factory.Inputs);
        Assert.Equal((Id("ore"), (BuildableId?)Id("miner")), (input.Inside, input.Outside));
        Assert.Equal([Items.IronOre], input.Items);
        Assert.Equal([Id("miner")], factory.Suppliers);
    }

    [Fact]
    public void A_bus_that_feeds_machines_of_two_factories_connects_them_and_belongs_to_neither()
    {
        // smelter → ingots → split → (to plates → constructor, to rods → rod maker). The rod maker is another factory's.
        TestWorld world = new TestWorld()
            .Miner("miner").Belt("ore").Machine("smelter", Items.IronOre, Items.IronIngot).Belt("ingots").Splitter("split")
            .Belt("to plates").Machine("constructor", Items.IronIngot, Items.IronPlate)
            .Belt("to rods").Machine("rod maker", Items.IronIngot, Items.IronRod)
            .Connect("miner", "ore", "smelter", "ingots", "split", "to plates", "constructor")
            .Connect("split", "to rods", "rod maker");

        FactoryMembers factory = Trace(world, "smelter", "constructor");

        Assert.DoesNotContain(Id("ingots"), factory.Logistics);
        Assert.DoesNotContain(Id("split"), factory.Logistics);
        Assert.Contains(factory.Outputs, output => output.Inside == Id("smelter") && output.Outside == Id("ingots"));
        Assert.Contains(factory.Inputs, input => input.Inside == Id("constructor") && input.Outside == Id("to plates"));
    }

    /// <summary>
    /// Two encoders of two factories leave their dark matter residue in one pipe network; a collider of a third takes
    /// it. The network is fed by more than one factory, so it belongs to none of them.
    /// </summary>
    private static TestWorld ResidueNetwork() => new TestWorld()
        .Machine("encoder A", Items.IronIngot, Items.Coal).Pipe("pipe A")
        .Machine("encoder B", Items.IronIngot, Items.Coal).Pipe("pipe B")
        .PipeNetwork("network").Pipe("to collider").Machine("collider", Items.Coal, Items.IronPlate)
        .Connect("encoder A", "pipe A", "network", "to collider", "collider")
        .Connect("encoder B", "pipe B", "network");

    [Fact]
    public void A_byproduct_network_fed_by_several_factories_belongs_to_none_and_each_factorys_output_ends_where_it_joins()
    {
        FactoryMembers encoders = Trace(ResidueNetwork(), "encoder A");

        Assert.Empty(encoders.Logistics);
        FactoryConnection output = Assert.Single(encoders.Outputs);
        Assert.Equal((Id("encoder A"), (BuildableId?)Id("pipe A")), (output.Inside, output.Outside));
        Assert.Equal([Items.Coal], output.Items);
    }

    [Fact]
    public void The_factory_that_takes_from_a_shared_network_has_it_as_an_input()
    {
        FactoryMembers colliders = Trace(ResidueNetwork(), "collider");

        FactoryConnection input = Assert.Single(colliders.Inputs);
        Assert.Equal((Id("collider"), (BuildableId?)Id("to collider")), (input.Inside, input.Outside));
        Assert.Equal([Items.Coal], input.Items);
    }

    [Fact]
    public void A_belt_from_one_factorys_machine_straight_into_anothers_belongs_to_neither()
    {
        FactoryMembers factory = Trace(PlateLine(), "smelter");

        Assert.DoesNotContain(Id("ingots"), factory.Logistics);
        Assert.Contains(factory.Outputs, output => output.Inside == Id("smelter") && output.Outside == Id("ingots"));
    }

    [Fact]
    public void A_factory_has_an_output_for_its_product_and_one_for_its_byproduct()
    {
        // The refinery makes plates and leaves coal: plates go to storage, coal to a sink.
        TestWorld world = new TestWorld()
            .Container("ore box").Belt("in").Machine("refinery", [Items.IronOre], [Items.IronPlate, Items.Coal])
            .Belt("plates").Container("box").Belt("waste").Sink("sink")
            .Connect("ore box", "in", "refinery", "plates", "box")
            .Connect("refinery", "waste", "sink");

        FactoryMembers factory = Trace(world, "refinery");

        Assert.Equal(
            [("box", (string?)null), ("waste", "sink")],
            factory.Outputs.Select(output => (output.Inside.Id, output.Outside?.Id)).OrderBy(output => output.Item1, StringComparer.Ordinal));
    }

    [Fact]
    public void An_input_carries_what_its_machines_ask_for_and_do_not_get_from_inside_the_factory()
    {
        // The assembler asks for plates and screws; the screws come from the factory's own screw maker.
        TestWorld world = new TestWorld()
            .Station("station").Belt("plates").Belt("rods").Machine("screw maker", Items.IronRod, Items.Screw).Belt("screws")
            .Machine("assembler", [Items.IronPlate, Items.Screw], [Items.ReinforcedIronPlate]).Belt("out").Sink("sink")
            .Connect("station", "plates", "assembler", "out", "sink")
            .Connect("station", "rods", "screw maker", "screws", "assembler");

        FactoryMembers factory = Trace(world, "screw maker", "assembler");

        Assert.Equal([Items.IronPlate], factory.Inputs.Single(input => input.Inside == Id("plates")).Items);
    }

    [Fact]
    public void An_input_that_brings_back_what_the_factory_also_makes_still_carries_it()
    {
        // The smelter's ingots leave on a bus to two factories, and come back to the constructor from it.
        TestWorld world = new TestWorld()
            .Miner("miner").Belt("ore").Machine("smelter", Items.IronOre, Items.IronIngot).Belt("ingots").Splitter("split")
            .Belt("to plates").Machine("constructor", Items.IronIngot, Items.IronPlate)
            .Belt("to rods").Machine("rod maker", Items.IronIngot, Items.IronRod)
            .Connect("miner", "ore", "smelter", "ingots", "split", "to plates", "constructor")
            .Connect("split", "to rods", "rod maker");

        FactoryMembers factory = Trace(world, "smelter", "constructor");

        Assert.Equal([Items.IronIngot], factory.Inputs.Single(input => input.Inside == Id("constructor")).Items);
    }

    [Fact]
    public void A_container_nothing_feeds_on_its_line_is_an_input_and_a_supplier()
    {
        TestWorld world = new TestWorld()
            .Container("ore box", (Items.IronOre, 500)).Belt("ore").Machine("smelter", Items.IronOre, Items.IronIngot).Belt("ingots").Sink("sink")
            .Connect("ore box", "ore", "smelter", "ingots", "sink");

        FactoryMembers factory = Trace(world, "smelter");

        Assert.Equal((Id("ore box"), (BuildableId?)null), (Assert.Single(factory.Inputs).Inside, factory.Inputs[0].Outside));
        Assert.Equal([Id("ore box")], factory.Suppliers);
    }

    [Fact]
    public void Its_producers_are_its_machines_whose_output_leaves_the_factory()
    {
        Assert.Equal([Id("constructor")], Trace(PlateLine(), "smelter", "constructor").Producers);
    }

    [Fact]
    public void A_factory_can_be_traced_in_a_world_that_has_any_of_its_machines()
    {
        var definition = new FactoryDefinition([Id("smelter"), Id("gone")]);

        Assert.True(definition.IsTraceableIn(PlateLine().Build()));
    }

    [Fact]
    public void A_factory_cannot_be_traced_in_a_world_without_any_of_its_machines()
    {
        Assert.False(new FactoryDefinition([Id("gone")]).IsTraceableIn(PlateLine().Build()));
    }

    [Fact]
    public void A_factory_has_at_least_one_machine()
    {
        Assert.Throws<ArgumentException>(() => new FactoryDefinition([]));
    }

    /// <summary>
    /// rod maker → rods → nuclear plant → waste → waste box (a container at the end), with water from a pump. Rods
    /// stand in for fuel rods, so the rod maker has a recipe the test items know.
    /// </summary>
    private static TestWorld NuclearPlant() => new TestWorld()
        .Miner("ingots").Belt("to rod maker").Machine("rod maker", Items.IronIngot, Items.UraniumFuelRod).Belt("rods")
        .Miner("pump").Pipe("water")
        .Generator("plant", Items.UraniumFuelRod, 0.2m, Items.Water, 240, Items.UraniumWaste, 10).Belt("waste").Container("waste box")
        .Connect("ingots", "to rod maker", "rod maker", "rods", "plant", "waste", "waste box")
        .Connect("pump", "water", "plant");

    [Fact]
    public void A_generator_can_be_one_of_its_machines()
    {
        Assert.Equal(["plant", "rod maker"], Names(Trace(NuclearPlant(), "rod maker", "plant").Machines));
    }

    [Fact]
    public void A_factory_of_generators_only_can_be_traced()
    {
        Assert.True(new FactoryDefinition([Id("plant")]).IsTraceableIn(NuclearPlant().Build()));
    }

    [Fact]
    public void The_fuel_line_to_its_own_generator_is_its_own_and_no_output()
    {
        FactoryMembers factory = Trace(NuclearPlant(), "rod maker", "plant");

        Assert.Contains(Id("rods"), factory.Logistics);
        Assert.DoesNotContain(factory.Outputs, output => output.Items.Contains(Items.UraniumFuelRod));
    }

    [Fact]
    public void The_waste_its_generator_leaves_is_an_output()
    {
        FactoryMembers factory = Trace(NuclearPlant(), "rod maker", "plant");

        FactoryConnection output = Assert.Single(factory.Outputs);
        Assert.Equal(Id("waste box"), output.Inside);
        Assert.Equal([Items.UraniumWaste], output.Items);
        Assert.Contains(Id("plant"), factory.Producers);
    }

    [Fact]
    public void Its_generator_asks_for_its_fuel_and_water_from_outside()
    {
        FactoryMembers factory = Trace(NuclearPlant(), "plant");

        Assert.Contains(Items.UraniumFuelRod, Assert.Single(factory.Inputs, input => input.Outside == Id("rods")).Items);
        Assert.Contains(Items.Water, Assert.Single(factory.Inputs, input => input.Outside == Id("pump")).Items);
    }

    [Fact]
    public void A_generator_not_chosen_stays_the_end_of_the_line_and_the_fuel_into_it_an_output()
    {
        FactoryMembers factory = Trace(NuclearPlant(), "rod maker");

        Assert.Contains(Id("rods"), factory.Logistics);
        FactoryConnection output = Assert.Single(factory.Outputs);
        Assert.Equal((Id("rods"), (BuildableId?)Id("plant")), (output.Inside, output.Outside));
        Assert.Equal([Items.UraniumFuelRod], output.Items);
    }
}
