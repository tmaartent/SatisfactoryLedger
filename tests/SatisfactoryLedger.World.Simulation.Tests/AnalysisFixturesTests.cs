using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using SatisfactoryLedger.World.Contracts.Analysis;
using SatisfactoryLedger.World.Simulation;
using SatisfactoryLedger.World.Snapshots;
using SatisfactoryLedger.World.Tests;

namespace SatisfactoryLedger.World.Simulation.Tests;

/// <summary>
/// The stored analyses of the test worlds, which World.Tests reads instead of running the solver (issue #47): they must
/// be what the solver computes now. When the solver changes on purpose, run these tests with
/// REGENERATE_ANALYSIS_FIXTURES=1 to write the fixtures anew, and look at what changed.
/// </summary>
public class AnalysisFixturesTests
{
    public static TheoryData<string> Fixtures() => [TestWorldSnapshot.TestWorldAnalysisFixture, TestWorldSnapshot.WithGeneratorsAnalysisFixture];

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void A_test_worlds_stored_analysis_is_what_the_solver_computes(string fixture)
    {
        SnapshotDocument snapshot = fixture == TestWorldSnapshot.TestWorldAnalysisFixture ? TestWorldSnapshot.Document : TestWorldSnapshot.WithGenerators;
        string computed = AnalysisDocument.Compute(snapshot.ToFlowNetwork()).ToJson();
        if (Environment.GetEnvironmentVariable("REGENERATE_ANALYSIS_FIXTURES") == "1")
        {
            // Indented, so that a diff after regenerating shows what the solver changed.
            File.WriteAllText(
                Path.Combine(Source(), "..", "SatisfactoryLedger.World.Tests", fixture),
                JsonNode.Parse(computed)!.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
            return;
        }

        Assert.True(
            File.Exists(fixture) && AnalysisDocument.FromJson(File.ReadAllText(fixture)).ToJson() == computed,
            $"{fixture} is not what the solver computes now: run these tests with REGENERATE_ANALYSIS_FIXTURES=1, and look at the diff");
    }

    /// <summary>This test project's folder in the source tree, where the fixtures are written.</summary>
    private static string Source([CallerFilePath] string file = "") => Path.GetDirectoryName(file)!;
}
