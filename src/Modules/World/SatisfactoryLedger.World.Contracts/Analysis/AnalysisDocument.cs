using System.Text.Json;

namespace SatisfactoryLedger.World.Contracts.Analysis;

/// <summary>
/// The stored result of a snapshot's simulation (ADR-0017): what every machine does, the findings, the parts that
/// didn't settle, and what flows into every belt, lift and pipe that carries something. The world's findings and any
/// factory's findings are made from it without simulating again.
/// </summary>
/// <param name="Version">
/// The analysis version it was computed with. Raise <see cref="CurrentVersion"/> when a change to the analysis rules
/// changes results: every stored analysis is then computed again.
/// </param>
/// <param name="Into">Per belt, lift, pipe or container in a line that carries something: what flows into it, per item and minute.</param>
public sealed record AnalysisDocument(
    int Version,
    IReadOnlyList<MachineStateDocument> Machines,
    IReadOnlyList<BottleneckDocument> Bottlenecks,
    IReadOnlyList<UnusedSupplyDocument> UnusedSupply,
    IReadOnlyList<UnknownSupplyDocument> UnknownSupply,
    IReadOnlyList<UnsettledPartDocument> UnsettledParts,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> Into)
{
    /// <summary>
    /// Version 1: the analysis of steps 12 to 17. Version 2: a machine at 99.99% or more is running (BR-A11).
    /// Version 3: from 99.9%.
    /// Version 4: generators burning a known fuel take only what they burn and make their waste (BR-A8).
    /// Version 5: what a machine or generator holds counts as supplied while it lasts 30 minutes (BR-A17), and loops
    /// start as if running (BR-A10).
    /// Version 6: every input a machine is short of, and every product that can't leave, with their shares (BR-A11).
    /// Version 7: the sweeps start from the most that can flow (ADR-0026), so loops and regions that wait on each other
    /// are no longer found standing still.
    /// Version 8: a full inventory at the end of a line takes nothing, unless the save shows items still arriving
    /// (BR-A7); in a loop, what the loop makes comes back first, before what comes from outside it (BR-A10).
    /// Version 9: a merger input that sends less than its share leaves the rest to the others, also when it offers more
    /// (BR-A5).
    /// A stored analysis of an older version is computed again.
    /// </summary>
    public const int CurrentVersion = 9;

    public string ToJson() => JsonSerializer.Serialize(this, SimulationJson.Options);

    public static AnalysisDocument FromJson(string json) =>
        JsonSerializer.Deserialize<AnalysisDocument>(json, SimulationJson.Options) ?? throw new JsonException("An analysis document can't be empty");

    /// <summary>The buildables in a part of the world that didn't settle: their findings are approximate (BR-A10).</summary>
    public HashSet<string> Approximate() => [.. UnsettledParts.SelectMany(part => part.Buildables)];
}