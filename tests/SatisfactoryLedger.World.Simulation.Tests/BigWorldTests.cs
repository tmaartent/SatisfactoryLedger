using System.Diagnostics;

using SatisfactoryLedger.World.Domain.Flow.Objects;
using SatisfactoryLedger.World.Domain.Tests;

namespace SatisfactoryLedger.World.Simulation.Tests;

/// <summary>The linear programmes on the largest part of the owner's big world (step 15's fixture).</summary>
public class BigWorldTests(ITestOutputHelper output)
{
    [Fact]
    public void The_largest_part_of_your_big_world_is_solved()
    {
        FlowNetwork part = FlowText.ReadGzip("Fixtures/big-world-part.flow.gz");

        var clock = Stopwatch.StartNew();
        FlowResult result = LinearSteadyState.Of(part);
        output.WriteLine($"{part.Nodes.Count} nodes, {part.Edges.Count} connections: {clock.Elapsed.TotalSeconds:0.0} s, {result.Sweeps} rounds");

        Assert.Equal(15328, part.Nodes.Count);
        Assert.True(result.Settled);
    }
}
