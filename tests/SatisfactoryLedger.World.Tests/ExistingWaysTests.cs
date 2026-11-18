using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Arrangement;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.SaveFile;
using SatisfactoryLedger.World.Snapshots;

using static SatisfactoryLedger.World.Tests.TestWorldSnapshot;

namespace SatisfactoryLedger.World.Tests;

/// <summary>
/// The way an existing belt takes in the save from a building outside the arranged area to a factory inside it, and where
/// it crosses into the area (decided 2026-11-18): a belt leaving the floor joins it there.
/// </summary>
public class ExistingWaysTests
{
    [Fact]
    public void A_way_enters_the_area_where_it_crosses_its_edge()
    {
        // East along y = 0 from outside, then north inside: it crosses the west edge at x = 500.
        ExistingWay? way = ExistingWays.Entering(
            [new WorldPoint(0, 0), new WorldPoint(1000, 0), new WorldPoint(1000, 1000)], new WorldRectangle(500, -500, 2000, 2000));

        Assert.Equal(new WorldPoint(500, 0), way?.Entry);
        Assert.Equal([new WorldPoint(0, 0), new WorldPoint(500, 0)], way?.Outside);
    }

    [Fact]
    public void A_way_that_never_enters_the_area_has_no_entry()
    {
        Assert.Null(ExistingWays.Entering([new WorldPoint(0, 0), new WorldPoint(1000, 0)], new WorldRectangle(500, 500, 2000, 2000)));
    }

    [Fact]
    public void The_miners_belt_to_smelter_a_is_followed_from_the_miner_into_an_area_around_the_smelter()
    {
        PositionDocument smelter = Document.Buildables.Single(buildable => buildable.Id == SmelterA).Position!;
        PositionDocument miner = Document.Buildables.Single(buildable => buildable.Id == Miner).Position!;
        var area = new WorldRectangle(smelter.X - 600, smelter.Y - 600, smelter.X + 600, smelter.Y + 600);

        ExistingWay? way = ExistingWays.Into(
            Document.ToWorldGraph(), Document.Buildables.ToDictionary(buildable => buildable.Id), Document.PipeNeighbours(), Miner, new HashSet<string> { SmelterA }, area);

        Assert.NotNull(way);

        // It starts where the miner's belt does, by its output, not at the miner's middle (2026-11-18: a short diagonal
        // from each freight platform's middle to its belt).
        BuildableDocument belt = Document.Buildables.Single(buildable => Document.Connections.Any(connection => connection.From == Miner && connection.To == buildable.Id));
        PositionDocument start = belt.Curve![0].Location;
        Assert.Equal(new WorldPoint(start.X, start.Y), way.Outside[0]);
        Assert.NotEqual(new WorldPoint(miner.X, miner.Y), way.Outside[0]);
        Assert.True(
            way.Entry.X is var x && way.Entry.Y is var y && (Math.Abs(x - area.X0) < 1 || Math.Abs(x - area.X1) < 1 || Math.Abs(y - area.Y0) < 1 || Math.Abs(y - area.Y1) < 1),
            $"{way.Entry} isn't on the area's edge");
        Assert.True(way.Outside.Count > 2, "it follows the belt's curve, not a straight line");
    }

    [Fact]
    public void The_way_followed_is_the_shortest_not_the_one_of_fewest_buildings()
    {
        // miner → s1 → s2 → smelter straight east, or miner → detour (500 m north) → smelter: fewer buildings, much
        // longer (2026-11-18: a freight platform's rubber was followed round the outside of the area).
        static BuildableId Saved(string name) => BuildableId.Of(Id(name));
        static SavedPosition At(double x, double y) => new(x, y, 0);
        static SavedLogistics Belt(string name, double x, double y) => new(Saved(name), "Build_ConveyorBeltMk1_C", LogisticsKind.Belt) { Position = At(x, y) };
        var saved = new SavedWorld(
            new SaveInfo("Two ways", 517554, new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc)),
            [
                new SavedProvider(Saved("miner"), "Build_MinerMk1_C", null, ClockSpeed.Percent(100), "Desc_OreIron_C") { Position = At(0, 0) },
                Belt("s1", 3000, 0), Belt("s2", 6000, 0), Belt("detour", 5000, 50000),
                new SavedMachine(Saved("smelter"), "Build_SmelterMk1_C", null, ClockSpeed.Percent(100), 1) { Position = At(10000, 0) },
            ],
            [
                new Connection(Saved("miner"), Saved("detour")), new Connection(Saved("detour"), Saved("smelter")),
                new Connection(Saved("miner"), Saved("s1")), new Connection(Saved("s1"), Saved("s2")), new Connection(Saved("s2"), Saved("smelter")),
            ],
            []);
        SnapshotDocument snapshot = SnapshotDocument.FromJson(SnapshotDocument.Create(saved, TestWorldSnapshot.Catalog).ToJson());

        ExistingWay? way = ExistingWays.Into(
            snapshot.ToWorldGraph(),
            snapshot.Buildables.ToDictionary(buildable => buildable.Id),
            snapshot.PipeNeighbours(),
            Id("miner"),
            new HashSet<string> { Id("smelter") },
            new WorldRectangle(8000, -1000, 12000, 1000));

        Assert.Equal([0.0, 3000, 6000, 8000], way?.Outside.Select(point => point.X));
    }

    [Fact]
    public void A_pipe_is_followed_piece_by_piece_through_its_network()
    {
        // pump → a → b → c → refinery, east along y = 0, every 10 m. The model joins the pipes into one network, so only
        // the pipes touching each other say which way it runs (decided 2026-11-18).
        static BuildableId Saved(string name) => BuildableId.Of(Id(name));
        static SavedPosition At(double x) => new(x, 0, 0);
        static SavedLogistics Pipe(string name, double x) => new(Saved(name), "Build_Pipeline_C", LogisticsKind.Pipe) { Position = At(x) };
        BuildableId hub = BuildableId.Of($"PipeNetwork:{Id("a")}");
        var saved = new SavedWorld(
            new SaveInfo("A pipe", 517554, new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc)),
            [
                new SavedProvider(Saved("pump"), "Build_WaterPump_C", null, ClockSpeed.Percent(100), "Desc_Water_C") { Position = At(0) },
                Pipe("a", 1000), Pipe("b", 2000), Pipe("c", 3000),
                new SavedLogistics(hub, "PipeNetwork", LogisticsKind.PipeNetwork),
                new SavedMachine(Saved("refinery"), "Build_OilRefinery_C", null, ClockSpeed.Percent(100), 1) { Position = At(4000) },
            ],
            [
                new Connection(Saved("pump"), Saved("a")), new Connection(Saved("a"), hub),
                new Connection(hub, Saved("c")), new Connection(Saved("c"), Saved("refinery")),
            ],
            [])
        {
            PipeLinks = [(Saved("a"), Saved("b")), (Saved("b"), Saved("c"))],
        };
        SnapshotDocument snapshot = SnapshotDocument.FromJson(SnapshotDocument.Create(saved, TestWorldSnapshot.Catalog).ToJson());

        ExistingWay? way = ExistingWays.Into(
            snapshot.ToWorldGraph(),
            snapshot.Buildables.ToDictionary(buildable => buildable.Id),
            snapshot.PipeNeighbours(),
            Id("pump"),
            new HashSet<string> { Id("refinery") },
            new WorldRectangle(2500, -1000, 5000, 1000));

        Assert.Equal(new WorldPoint(2500, 0), way?.Entry);
        Assert.Equal([0.0, 1000, 2000, 2500], way?.Outside.Select(point => point.X));
    }
}
