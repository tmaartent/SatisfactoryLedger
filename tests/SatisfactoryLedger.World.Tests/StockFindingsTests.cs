using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Analysis;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.SaveFile;
using SatisfactoryLedger.World.Simulation;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Tests;

/// <summary>
/// Machines running on stock (BR-A17): what a machine holds counts as supplied while it lasts 30 minutes, and its
/// finding says how long it lasts. A small world: a smelter (30 ore a minute) with nothing bringing ore, and one fed
/// from a container.
/// </summary>
public class StockFindingsTests
{
    private const string Ore = "Desc_OreIron_C";
    private static readonly string Holding = TestWorldSnapshot.Id("Build_SmelterMk1_C_1");
    private static readonly string Fed = TestWorldSnapshot.Id("Build_SmelterMk1_C_2");

    private static AnalysisResponse Analyse(decimal holds)
    {
        static BuildableId Id(string name) => BuildableId.Of(TestWorldSnapshot.Id(name));
        SavedMachine Smelter(string id, decimal ore) =>
            new(BuildableId.Of(id), "Build_SmelterMk1_C", "Recipe_IngotIron_C", ClockSpeed.Percent(100), 1)
            {
                InputContents = new Dictionary<string, decimal> { [Ore] = ore },
            };
        SavedLogistics Belt(string name) => new(Id(name), "Build_ConveyorBeltMk1_C", LogisticsKind.Belt);

        var saved = new SavedWorld(
            new SaveInfo("Stock", 517554, new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc)),
            [
                Smelter(Holding, holds), Belt("out1"), new SavedConsumer(Id("sink1"), "Build_ResourceSink_C"),
                new SavedLogistics(Id("box"), "Build_StorageContainerMk1_C", LogisticsKind.StorageContainer)
                {
                    Stock = new Dictionary<string, decimal> { [Ore] = 5_000 },
                },
                Belt("in2"), Smelter(Fed, 900), Belt("out2"), new SavedConsumer(Id("sink2"), "Build_ResourceSink_C"),
            ],
            [
                new Connection(BuildableId.Of(Holding), Id("out1")), new Connection(Id("out1"), Id("sink1")),
                new Connection(Id("box"), Id("in2")), new Connection(Id("in2"), BuildableId.Of(Fed)),
                new Connection(BuildableId.Of(Fed), Id("out2")), new Connection(Id("out2"), Id("sink2")),
            ],
            []);
        SnapshotDocument snapshot = SnapshotDocument.FromJson(SnapshotDocument.Create(saved, TestWorldSnapshot.Catalog).ToJson());
        return SnapshotAnalysis.For(snapshot, AnalysisDocument.FromJson(AnalysisDocument.Compute(snapshot.ToFlowNetwork()).ToJson()));
    }

    private static MachineFinding Machine(AnalysisResponse analysis, string id) => Assert.Single(analysis.Machines, machine => machine.Id == id);

    [Fact]
    public void A_machine_holding_half_an_hour_of_its_input_runs_on_that_stock()
    {
        // 900 ore at 30 a minute.
        MachineFinding smelter = Machine(Analyse(holds: 900), Holding);

        Assert.Equal((1.0, MachineLimit.None, Ore, 30.0), (smelter.Utilisation, smelter.Limit, smelter.OnStock, smelter.StockMinutes));
    }

    [Fact]
    public void A_machine_holding_less_is_starved_like_before()
    {
        MachineFinding smelter = Machine(Analyse(holds: 899), Holding);

        Assert.Equal((MachineLimit.Starved, (string?)null), (smelter.Limit, smelter.OnStock));
    }

    [Fact]
    public void A_starved_machine_names_every_input_it_is_short_of_with_the_share_it_is_offered()
    {
        MachineFinding smelter = Machine(Analyse(holds: 899), Holding);

        Assert.Equal(new Dictionary<string, double> { [Ore] = 0 }, smelter.StarvedOf);
        Assert.Empty(smelter.BlockedOn);
    }

    [Fact]
    public void A_machine_whose_belts_bring_all_it_uses_is_not_running_on_stock()
    {
        MachineFinding smelter = Machine(Analyse(holds: 900), Fed);

        Assert.Equal((1.0, (string?)null, (double?)null), (smelter.Utilisation, smelter.OnStock, smelter.StockMinutes));
    }
}
