using SatisfactoryLedger.Catalog.Domain.GameObjects;

namespace SatisfactoryLedger.Catalog.Domain.Tests.GameObjects;

/// <summary>A pump lifts fluid by its head lift, from the Docs file (asked 2026-11-24).</summary>
public class PumpTests
{
    private static Pump Mk1() => Pump.Create(PumpId.Of("Build_PipelinePump_C"), "Pipeline Pump Mk.1", headLiftMetres: 20);

    [Fact]
    public void A_pump_lifts_fluid_by_its_head_lift()
    {
        Assert.Equal(20m, Mk1().HeadLiftMetres);
    }

    [Fact]
    public void A_pump_lifts_fluid_some_height()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Pump.Create(PumpId.Of("Build_Valve_C"), "Valve", headLiftMetres: 0));
    }

    [Fact]
    public void A_pump_from_a_newer_game_file_takes_its_head_lift()
    {
        Pump stored = Mk1();
        stored.MarkNoLongerInGame();

        stored.UpdateFromGame(Pump.Create(PumpId.Of("Build_PipelinePump_C"), "Pipeline Pump Mk.1", 25));

        Assert.Equal((25m, true), (stored.HeadLiftMetres, stored.IsInGame));
    }
}
