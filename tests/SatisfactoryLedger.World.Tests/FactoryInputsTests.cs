using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Analysis;
using SatisfactoryLedger.World.Contracts.Analysis;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Factories;
using SatisfactoryLedger.World.SaveFile;
using SatisfactoryLedger.World.Simulation;
using SatisfactoryLedger.World.Snapshots;

using static SatisfactoryLedger.World.Tests.TestWorldSnapshot;

namespace SatisfactoryLedger.World.Tests;

/// <summary>
/// What a factory takes in per item, and where it comes from (decided 2026-11-11), in the test world. Constructor C,
/// clocked at 75%, takes 22.5 ingots a minute: 12.5 from smelter A, 10 from smelter B.
/// </summary>
public class FactoryInputsTests
{
    private static StoredFactory Factory(SnapshotDocument snapshot, string name, params string[] machines) => new()
    {
        Id = Guid.CreateVersion7(),
        World = "Development",
        Name = name,
        Machines = [.. machines.Select(id => StoredMachine.From(snapshot.Buildables.Single(buildable => buildable.Id == id)))],
    };

    private static readonly StoredFactory Smelting = Factory(Document, "Smelting", SmelterA);
    private static readonly StoredFactory Plates = Factory(Document, "Plates", ConstructorC);

    private static FactoryInput Input(StoredFactory factory, string item, params StoredFactory[] factories) =>
        Assert.Single(SnapshotAnalysis.ForFactory(Document, factory, TestWorldAnalysis, factories)!.Input, input => input.Item == item);

    [Fact]
    public void It_takes_now_what_arrives_over_its_inputs_and_at_100_percent_what_its_machines_ask_for()
    {
        FactoryInput ingots = Input(Plates, "Desc_IronIngot_C", Smelting, Plates);

        Assert.Equal((22.5, 22.5, 0.0), (ingots.Now, ingots.AtFullSpeed, ingots.Short));
    }

    [Fact]
    public void What_arrives_comes_from_another_factory_and_machines_of_no_factory_in_proportion_to_what_each_brings()
    {
        FactoryInput ingots = Input(Plates, "Desc_IronIngot_C", Smelting, Plates);

        Assert.Equal(
            [(InputSourceKind.Factory, "Smelting", 12.5), (InputSourceKind.NoFactory, (string?)null, 10.0)],
            ingots.Sources.Select(source => (source.Kind, source.Factory, source.PerMinute)));
    }

    [Fact]
    public void Ore_from_a_miner_comes_from_that_building()
    {
        StoredFactory ironPlates = Factory(Document, "Iron plates", SmelterA, SmelterB, ConstructorC);

        InputSource source = Assert.Single(Input(ironPlates, "Desc_OreIron_C", ironPlates).Sources);

        Assert.Equal((InputSourceKind.Building, "Build_MinerMk1_C", Miner), (source.Kind, source.Building, source.Id));
    }

    [Fact]
    public void Two_miners_merging_onto_one_belt_are_two_sources()
    {
        // miner A → a ─┐
        //              merge → ore → smelter → ingots → box
        // miner B → b ─┘
        static BuildableId Saved(string name) => BuildableId.Of(Id(name));
        SavedProvider Miner(string name) => new(Saved(name), "Build_MinerMk1_C", "BP_ResourceNode35", ClockSpeed.Percent(100), "Desc_OreIron_C");
        SavedLogistics Belt(string name) => new(Saved(name), "Build_ConveyorBeltMk1_C", LogisticsKind.Belt);
        var saved = new SavedWorld(
            new SaveInfo("Two miners", 517554, new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc)),
            [
                Miner("miner A"), Belt("a"), Miner("miner B"), Belt("b"),
                new SavedLogistics(Saved("merge"), "Build_ConveyorAttachmentMerger_C", LogisticsKind.Merger), Belt("ore"),
                new SavedMachine(Saved("smelter"), "Build_SmelterMk1_C", "Recipe_IngotIron_C", ClockSpeed.Percent(100), 1),
                Belt("ingots"), new SavedLogistics(Saved("box"), "Build_StorageContainerMk1_C", LogisticsKind.StorageContainer),
            ],
            [
                new Connection(Saved("miner A"), Saved("a")), new Connection(Saved("a"), Saved("merge")),
                new Connection(Saved("miner B"), Saved("b")), new Connection(Saved("b"), Saved("merge")),
                new Connection(Saved("merge"), Saved("ore")), new Connection(Saved("ore"), Saved("smelter")),
                new Connection(Saved("smelter"), Saved("ingots")), new Connection(Saved("ingots"), Saved("box")),
            ],
            []);
        SnapshotDocument snapshot = SnapshotDocument.FromJson(SnapshotDocument.Create(saved, TestWorldSnapshot.Catalog).ToJson());
        AnalysisDocument analysis = AnalysisDocument.FromJson(AnalysisDocument.Compute(snapshot.ToFlowNetwork()).ToJson());
        StoredFactory smelting = Factory(snapshot, "Smelting", Id("smelter"));

        FactoryInput ore = Assert.Single(SnapshotAnalysis.ForFactory(snapshot, smelting, analysis, [smelting])!.Input);

        Assert.Equal([Id("miner A"), Id("miner B")], ore.Sources.Select(source => source.Id!).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void It_is_not_short_when_its_sources_have_the_rest_to_spare_and_the_line_has_room_for_it()
    {
        // miner → ore → smelter → dead end: the smelter's ingots can't leave, so it stands still and no ore arrives;
        // but the miner has ore to spare and the belt room for it, so at 100% it would get what it needs.
        static BuildableId Saved(string name) => BuildableId.Of(Id(name));
        var saved = new SavedWorld(
            new SaveInfo("Standing still", 517554, new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc)),
            [
                new SavedProvider(Saved("miner"), "Build_MinerMk1_C", "BP_ResourceNode35", ClockSpeed.Percent(100), "Desc_OreIron_C"),
                new SavedLogistics(Saved("ore"), "Build_ConveyorBeltMk2_C", LogisticsKind.Belt),
                new SavedMachine(Saved("smelter"), "Build_SmelterMk1_C", "Recipe_IngotIron_C", ClockSpeed.Percent(100), 1),
                new SavedLogistics(Saved("dead end"), "Build_ConveyorBeltMk1_C", LogisticsKind.Belt),
            ],
            [
                new Connection(Saved("miner"), Saved("ore")), new Connection(Saved("ore"), Saved("smelter")),
                new Connection(Saved("smelter"), Saved("dead end")),
            ],
            []);
        SnapshotDocument snapshot = SnapshotDocument.FromJson(SnapshotDocument.Create(saved, TestWorldSnapshot.Catalog).ToJson());
        AnalysisDocument analysis = AnalysisDocument.FromJson(AnalysisDocument.Compute(snapshot.ToFlowNetwork()).ToJson());
        StoredFactory smelting = Factory(snapshot, "Smelting", Id("smelter"));

        FactoryInput ore = Assert.Single(SnapshotAnalysis.ForFactory(snapshot, smelting, analysis, [smelting])!.Input);

        Assert.True(ore.Now < ore.AtFullSpeed, $"it takes {ore.Now} of {ore.AtFullSpeed}");
        Assert.Equal(0, ore.Short);
    }

    [Fact]
    public void It_is_not_short_of_what_overflows_into_a_sink_over_a_merger()
    {
        // miner → line → split → (to A → smelter A, to B → smelter B, overflow → merge → into sink → sink). The split
        // shares the miner's 60 three ways, so smelter A, the factory, gets less than its 30; what overflows into the
        // sink could come to it instead. Smelter B is no factory's, so the line is shared and the sink outside.
        static BuildableId Saved(string name) => BuildableId.Of(Id(name));
        static SavedLogistics Belt(string name) => new(Saved(name), "Build_ConveyorBeltMk2_C", LogisticsKind.Belt);
        static SavedMachine Smelter(string name) => new(Saved(name), "Build_SmelterMk1_C", "Recipe_IngotIron_C", ClockSpeed.Percent(100), 1);
        var saved = new SavedWorld(
            new SaveInfo("Overflow", 517554, new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc)),
            [
                new SavedProvider(Saved("miner"), "Build_MinerMk1_C", "BP_ResourceNode35", ClockSpeed.Percent(100), "Desc_OreIron_C"),
                Belt("line"), new SavedLogistics(Saved("split"), "Build_ConveyorAttachmentSplitter_C", LogisticsKind.Splitter),
                Belt("to A"), Smelter("smelter A"), Belt("ingots A"), new SavedConsumer(Saved("sink A"), "Build_ResourceSink_C"),
                Belt("to B"), Smelter("smelter B"), Belt("ingots B"), new SavedConsumer(Saved("sink B"), "Build_ResourceSink_C"),
                Belt("overflow"), new SavedLogistics(Saved("merge"), "Build_ConveyorAttachmentMerger_C", LogisticsKind.Merger),
                Belt("into sink"), new SavedConsumer(Saved("sink"), "Build_ResourceSink_C"),
            ],
            [
                new Connection(Saved("miner"), Saved("line")), new Connection(Saved("line"), Saved("split")),
                new Connection(Saved("split"), Saved("to A")), new Connection(Saved("to A"), Saved("smelter A")),
                new Connection(Saved("smelter A"), Saved("ingots A")), new Connection(Saved("ingots A"), Saved("sink A")),
                new Connection(Saved("split"), Saved("to B")), new Connection(Saved("to B"), Saved("smelter B")),
                new Connection(Saved("smelter B"), Saved("ingots B")), new Connection(Saved("ingots B"), Saved("sink B")),
                new Connection(Saved("split"), Saved("overflow")), new Connection(Saved("overflow"), Saved("merge")),
                new Connection(Saved("merge"), Saved("into sink")), new Connection(Saved("into sink"), Saved("sink")),
            ],
            []);
        SnapshotDocument snapshot = SnapshotDocument.FromJson(SnapshotDocument.Create(saved, TestWorldSnapshot.Catalog).ToJson());
        AnalysisDocument analysis = AnalysisDocument.FromJson(AnalysisDocument.Compute(snapshot.ToFlowNetwork()).ToJson());
        StoredFactory smelting = Factory(snapshot, "Smelting", Id("smelter A"));

        FactoryInput ore = Assert.Single(SnapshotAnalysis.ForFactory(snapshot, smelting, analysis, [smelting])!.Input);

        Assert.True(ore.Now < ore.AtFullSpeed, $"it takes {ore.Now} of {ore.AtFullSpeed}");
        Assert.Equal(0, ore.Short);
    }

    [Fact]
    public void A_sink_on_an_overflow_that_carries_nothing_has_nothing_to_give()
    {
        // miner → line → smart split → (any: main → split → three smelters; overflow: spill → sink). The three smelters
        // would take 90 a minute and take all the miner's 60, so nothing overflows: the sink has nothing they could get.
        static BuildableId Saved(string name) => BuildableId.Of(Id(name));
        static SavedLogistics Belt(string name) => new(Saved(name), "Build_ConveyorBeltMk2_C", LogisticsKind.Belt);
        static SavedMachine Smelter(string name) => new(Saved(name), "Build_SmelterMk1_C", "Recipe_IngotIron_C", ClockSpeed.Percent(100), 1);
        List<SavedBuildable> buildables =
        [
            new SavedProvider(Saved("miner"), "Build_MinerMk1_C", "BP_ResourceNode35", ClockSpeed.Percent(100), "Desc_OreIron_C"),
            Belt("line"),
            new SavedLogistics(Saved("smart"), "Build_ConveyorAttachmentSplitterSmart_C", LogisticsKind.Splitter,
                [new SortRule(0, Domain.SortRuleKind.Any), new SortRule(1, Domain.SortRuleKind.Overflow)]),
            Belt("main"), new SavedLogistics(Saved("split"), "Build_ConveyorAttachmentSplitter_C", LogisticsKind.Splitter),
            Belt("spill"), new SavedConsumer(Saved("sink"), "Build_ResourceSink_C"),
        ];
        List<Connection> connections =
        [
            new(Saved("miner"), Saved("line")), new(Saved("line"), Saved("smart")),
            new(Saved("smart"), Saved("main"), FromPort: 0), new(Saved("main"), Saved("split")),
            new(Saved("smart"), Saved("spill"), FromPort: 1), new(Saved("spill"), Saved("sink")),
        ];
        foreach (string n in new[] { "1", "2", "3" })
        {
            buildables.AddRange([Belt("to " + n), Smelter("smelter " + n), Belt("ingots " + n), new SavedConsumer(Saved("box " + n), "Build_ResourceSink_C")]);
            connections.AddRange([new(Saved("split"), Saved("to " + n)), new(Saved("to " + n), Saved("smelter " + n)),
                new(Saved("smelter " + n), Saved("ingots " + n)), new(Saved("ingots " + n), Saved("box " + n))]);
        }

        var saved = new SavedWorld(new SaveInfo("Nothing spills", 517554, new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc)), buildables, connections, []);
        SnapshotDocument snapshot = SnapshotDocument.FromJson(SnapshotDocument.Create(saved, TestWorldSnapshot.Catalog).ToJson());
        AnalysisDocument analysis = AnalysisDocument.FromJson(AnalysisDocument.Compute(snapshot.ToFlowNetwork()).ToJson());
        StoredFactory smelting = Factory(snapshot, "Smelting", Id("smelter 1"), Id("smelter 2"), Id("smelter 3"));

        FactoryInput ore = Assert.Single(SnapshotAnalysis.ForFactory(snapshot, smelting, analysis, [smelting])!.Input);

        Assert.Equal(ore.AtFullSpeed - ore.Now, ore.Short, 4);
        Assert.True(ore.Short > 0, $"it takes {ore.Now} of {ore.AtFullSpeed}");
    }

    /// <summary>
    /// platform → in → [box →  on →] smelter → dead end: the smelter stands still, so it takes less than its 30 a
    /// minute; a Freight Platform has spare as far as its stock, and that of the containers on its line, lasts 30
    /// minutes at the higher rate (decided 2026-11-11).
    /// </summary>
    private static FactoryInput OreFromAPlatform(decimal onPlatform, decimal inBox)
    {
        static BuildableId Saved(string name) => BuildableId.Of(Id(name));
        static SavedLogistics Belt(string name) => new(Saved(name), "Build_ConveyorBeltMk2_C", LogisticsKind.Belt);
        var saved = new SavedWorld(
            new SaveInfo("Platform", 517554, new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc)),
            [
                new SavedProvider(Saved("platform"), "Build_TrainDockingStation_C", Node: null, ClockSpeed.Percent(100))
                {
                    Stock = new Dictionary<string, decimal> { ["Desc_OreIron_C"] = onPlatform },
                },
                Belt("in"),
                new SavedLogistics(Saved("box"), "Build_StorageContainerMk2_C", LogisticsKind.StorageContainer)
                {
                    Stock = new Dictionary<string, decimal> { ["Desc_OreIron_C"] = inBox },
                },
                Belt("on"),
                new SavedMachine(Saved("smelter"), "Build_SmelterMk1_C", "Recipe_IngotIron_C", ClockSpeed.Percent(100), 1),
                new SavedLogistics(Saved("dead end"), "Build_ConveyorBeltMk1_C", LogisticsKind.Belt),
            ],
            [
                new Connection(Saved("platform"), Saved("in")), new Connection(Saved("in"), Saved("box")),
                new Connection(Saved("box"), Saved("on")), new Connection(Saved("on"), Saved("smelter")),
                new Connection(Saved("smelter"), Saved("dead end")),
            ],
            []);
        SnapshotDocument snapshot = SnapshotDocument.FromJson(SnapshotDocument.Create(saved, TestWorldSnapshot.Catalog).ToJson());
        AnalysisDocument analysis = AnalysisDocument.FromJson(AnalysisDocument.Compute(snapshot.ToFlowNetwork()).ToJson());
        StoredFactory smelting = Factory(snapshot, "Smelting", Id("smelter"));
        return Assert.Single(SnapshotAnalysis.ForFactory(snapshot, smelting, analysis, [smelting])!.Input);
    }

    [Fact]
    public void A_platform_whose_stock_lasts_half_an_hour_at_the_higher_rate_has_it_to_spare()
    {
        Assert.Equal(0, OreFromAPlatform(onPlatform: 9_000, inBox: 0).Short);
    }

    [Fact]
    public void A_platform_has_to_spare_only_what_its_stock_keeps_up_for_half_an_hour()
    {
        // 300 ore last half an hour at 10 a minute: 20 of the smelter's 30 can't come.
        Assert.Equal(20, OreFromAPlatform(onPlatform: 300, inBox: 0).Short, 2);
    }

    [Fact]
    public void The_containers_a_platform_unloads_into_count_with_its_stock()
    {
        Assert.Equal(0, OreFromAPlatform(onPlatform: 0, inBox: 9_000).Short);
    }

    [Fact]
    public void At_100_percent_it_needs_from_outside_only_what_its_own_machines_do_not_make()
    {
        // Smelter A's ingots reach constructor C over the line smelter B shares, so ingots are an input; but A makes 30
        // a minute at 100% and C takes 22.5, so none are needed from outside.
        StoredFactory smeltAndPress = Factory(Document, "Smelt and press", SmelterA, ConstructorC);

        FactoryInput ingots = Input(smeltAndPress, "Desc_IronIngot_C", smeltAndPress);

        Assert.Equal((22.5, 0.0, 0.0), (ingots.Now, ingots.AtFullSpeed, ingots.Short));
    }
}
