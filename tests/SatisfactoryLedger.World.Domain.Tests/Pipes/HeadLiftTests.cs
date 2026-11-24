using SatisfactoryLedger.World.Domain.Pipes;

namespace SatisfactoryLedger.World.Domain.Tests.Pipes;

/// <summary>
/// BR-A22: how high fluid reaches in a pipe network, estimated: a fluid output lifts it 10 m above the pipe's end at
/// the output, a pump its head lift above the pump. Heights in metres.
/// </summary>
public class HeadLiftTests
{
    private static readonly BuildableId A = BuildableId.Of("a");
    private static readonly BuildableId B = BuildableId.Of("b");
    private static readonly BuildableId C = BuildableId.Of("c");
    private static readonly BuildableId Pump = BuildableId.Of("pump");

    private static IReadOnlyDictionary<BuildableId, double> Reaches(
        IEnumerable<PipePiece> pieces, IEnumerable<(BuildableId, BuildableId)> links, params FluidOutput[] outputs) =>
        HeadLift.Reaches([.. pieces], links, outputs);

    [Fact]
    public void A_fluid_output_lifts_fluid_ten_metres_above_where_its_pipe_starts()
    {
        var reaches = Reaches([new PipePiece(A, TopMetres: 8)], [], new FluidOutput(A, HeightMetres: 3));

        Assert.Equal(13, reaches[A]);
    }

    [Fact]
    public void Fluid_reaches_as_high_along_every_pipe_it_flows_through()
    {
        var reaches = Reaches([new PipePiece(A, 3), new PipePiece(B, 0), new PipePiece(C, 25)], [(A, B), (B, C)], new FluidOutput(A, 3));

        Assert.Equal((13, 13), (reaches[B], reaches[C]));
    }

    [Fact]
    public void A_pump_lifts_fluid_its_head_lift_above_itself()
    {
        PipePiece pump = new(Pump, TopMetres: 30, new PumpLift(HeadLiftMetres: 20, Outlet: B));

        var reaches = Reaches([new PipePiece(A, 0), pump, new PipePiece(B, 45)], [(A, Pump), (Pump, B)], new FluidOutput(A, 0));

        Assert.Equal(50, reaches[B]);
    }

    [Fact]
    public void A_pumps_head_lift_does_not_add_to_what_is_left_of_the_fluid_outputs()
    {
        PipePiece pump = new(Pump, TopMetres: 0, new PumpLift(20, Outlet: B));

        var reaches = Reaches([new PipePiece(A, 0), pump, new PipePiece(B, 0)], [(A, Pump), (Pump, B)], new FluidOutput(A, 0));

        Assert.Equal(20, reaches[B]);
    }

    [Fact]
    public void Past_a_pump_fluid_still_reaches_as_high_as_before_it_when_that_is_higher()
    {
        PipePiece pump = new(Pump, TopMetres: 0, new PumpLift(20, Outlet: B));

        var reaches = Reaches([new PipePiece(A, 40), pump, new PipePiece(B, 0)], [(A, Pump), (Pump, B)], new FluidOutput(A, 40));

        Assert.Equal(50, reaches[B]);
    }

    [Fact]
    public void A_pump_does_not_pass_fluid_back_against_its_direction()
    {
        PipePiece pump = new(Pump, TopMetres: 0, new PumpLift(20, Outlet: B));

        var reaches = Reaches([new PipePiece(A, 0), pump, new PipePiece(B, 0)], [(A, Pump), (Pump, B)], new FluidOutput(B, 0));

        Assert.False(reaches.ContainsKey(A));
    }

    [Fact]
    public void A_pump_whose_direction_is_not_known_passes_fluid_both_ways_without_lifting_it()
    {
        PipePiece pump = new(Pump, TopMetres: 0, Lift: null);

        var reaches = Reaches([new PipePiece(A, 0), pump, new PipePiece(B, 0)], [(A, Pump), (Pump, B)], new FluidOutput(B, 0));

        Assert.Equal(10, reaches[A]);
    }

    [Fact]
    public void Where_two_fluid_outputs_feed_a_pipe_the_higher_reach_counts()
    {
        var reaches = Reaches([new PipePiece(A, 0), new PipePiece(B, 0)], [(A, B)], new FluidOutput(A, 0), new FluidOutput(B, 6));

        Assert.Equal(16, reaches[A]);
    }

    [Fact]
    public void Gas_needs_no_head_lift_so_a_pipe_carrying_gas_has_no_reach()
    {
        var reaches = Reaches([new PipePiece(A, 0, CarriesGas: true), new PipePiece(B, 0, CarriesGas: true)], [(A, B)], new FluidOutput(A, 0));

        Assert.Empty(reaches);
    }

    [Fact]
    public void A_pipe_no_fluid_output_feeds_has_no_reach()
    {
        var reaches = Reaches([new PipePiece(A, 0), new PipePiece(B, 0)], [], new FluidOutput(A, 0));

        Assert.False(reaches.ContainsKey(B));
    }

    [Fact]
    public void A_pipe_has_head_lift_to_spare_while_the_reach_is_above_its_top()
    {
        Assert.Equal(6, new PipePiece(A, TopMetres: 14).Headroom(reachMetres: 20));
    }

    [Fact]
    public void A_pipe_whose_top_is_above_the_reach_is_short_of_head_lift()
    {
        Assert.Equal(-4, new PipePiece(A, TopMetres: 14).Headroom(reachMetres: 10));
    }
}
