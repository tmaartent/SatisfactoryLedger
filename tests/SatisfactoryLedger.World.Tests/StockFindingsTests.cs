using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Analysis;
using SatisfactoryLedger.World.Contracts.Analysis;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.SaveFile;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Tests;

/// <summary>
/// Machines running on stock (BR-A17): a machine whose belts bring less than it uses says so, with how long its stock
/// lasts. A small world: a smelter (30 ore a minute) holding 900 ore with nothing bringing ore, and one fed from a
/// container. Both run at full speed in its analysis, built by hand; that the solver lets a machine run on half an hour
/// of stock is tested end to end in World.Simulation.Tests.
/// </summary>
public class StockFindingsTests
{
    private const string Ore = "Desc_OreIron_C";
    private static readonly string Holding = TestWorldSnapshot.Id("Build_SmelterMk1_C_1");
    private static readonly string Fed = TestWorldSnapshot.Id("Build_SmelterMk1_C_2");

    private static readonly AnalysisResponse Analysis = Analyse();

    private static AnalysisResponse Analyse()
    {
        static string Id(string name) => TestWorldSnapshot.Id(name);
        SavedMachine Smelter(string id) =>
            new(BuildableId.Of(id), "Build_SmelterMk1_C", "Recipe_IngotIron_C", ClockSpeed.Percent(100), 1)
            {
                InputContents = new Dictionary<string, decimal> { [Ore] = 900 },
            };
        SavedLogistics Belt(string name) => new(BuildableId.Of(Id(name)), "Build_ConveyorBeltMk1_C", LogisticsKind.Belt);
        Connection Link(string from, string to) => new(BuildableId.Of(from), BuildableId.Of(to));

        var saved = new SavedWorld(
            new SaveInfo("Stock", 517554, new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc)),
            [
                Smelter(Holding), Belt("out1"), new SavedConsumer(BuildableId.Of(Id("sink1")), "Build_ResourceSink_C"),
                new SavedLogistics(BuildableId.Of(Id("box")), "Build_StorageContainerMk1_C", LogisticsKind.StorageContainer)
                {
                    Stock = new Dictionary<string, decimal> { [Ore] = 5_000 },
                },
                Belt("in2"), Smelter(Fed), Belt("out2"), new SavedConsumer(BuildableId.Of(Id("sink2")), "Build_ResourceSink_C"),
            ],
            [
                Link(Holding, Id("out1")), Link(Id("out1"), Id("sink1")),
                Link(Id("box"), Id("in2")), Link(Id("in2"), Fed), Link(Fed, Id("out2")), Link(Id("out2"), Id("sink2")),
            ],
            []);
        SnapshotDocument snapshot = SnapshotDocument.FromJson(SnapshotDocument.Create(saved, TestWorldSnapshot.Catalog).ToJson());
        // Both smelters run at full speed: one on its stock, the other on the 30 ore a minute its belt brings.
        AnalysisDocument analysis = new TestAnalysis().Running(Holding).Running(Fed).Into(Id("in2"), Ore, 30).Build();
        return SnapshotAnalysis.For(snapshot, analysis);
    }

    private static MachineFinding Machine(string id) => Assert.Single(Analysis.Machines, machine => machine.Id == id);

    [Fact]
    public void A_machine_running_on_its_stock_says_so_with_how_long_it_lasts()
    {
        // 900 ore at 30 a minute, with nothing coming in.
        MachineFinding smelter = Machine(Holding);

        Assert.Equal((Ore, 30.0), (smelter.OnStock, smelter.StockMinutes));
    }

    [Fact]
    public void A_machine_whose_belts_bring_all_it_uses_is_not_running_on_stock()
    {
        MachineFinding smelter = Machine(Fed);

        Assert.Equal(((string?)null, (double?)null), (smelter.OnStock, smelter.StockMinutes));
    }
}
