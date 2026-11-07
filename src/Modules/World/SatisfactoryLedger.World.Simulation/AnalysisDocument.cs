using System.Text.Json;

using SatisfactoryLedger.World.Domain.Flow;

namespace SatisfactoryLedger.World.Simulation;

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
    /// A stored analysis of an older version is computed again.
    /// </summary>
    public const int CurrentVersion = 4;

    /// <summary>Simulates a world's flow network and keeps what the findings need.</summary>
    public static AnalysisDocument Compute(FlowNetwork network)
    {
        ArgumentNullException.ThrowIfNull(network);
        FlowResult result = SteadyState.Of(network);

        Dictionary<string, IReadOnlyDictionary<string, double>> into = new(StringComparer.Ordinal);
        foreach (FlowConduit conduit in network.Nodes.OfType<FlowConduit>())
        {
            Dictionary<string, double> flowing = result.Into(conduit.Id)
                .Where(entry => entry.Value > SteadyState.Tolerance)
                .ToDictionary(entry => entry.Key.Id, entry => entry.Value, StringComparer.Ordinal);
            if (flowing.Count > 0)
            {
                into[conduit.Id.Id] = flowing;
            }
        }

        return new AnalysisDocument(
            CurrentVersion,
            [.. result.Machines.Select(machine => new MachineStateDocument(machine.Machine.Id, machine.Utilisation, machine.Limit, machine.LimitingItem?.Id))],
            [.. result.Bottlenecks.Select(bottleneck => new BottleneckDocument(bottleneck.Conduit.Id, bottleneck.Capacity, bottleneck.Flow, bottleneck.OfferedMore))],
            [.. result.UnusedSupply.Select(supply => new UnusedSupplyDocument(supply.Provider.Id, supply.Item.Id, supply.Offered, supply.Delivered))],
            [
                .. result.UnknownSupply.Select(supply => new UnknownSupplyDocument(
                    supply.Source.Id,
                    supply.Delivered.ToDictionary(entry => entry.Key.Id, entry => entry.Value, StringComparer.Ordinal),
                    supply.StockMinutes,
                    supply.Confirmed))
            ],
            [.. result.Parts.Where(part => !part.Settled).Select(part => new UnsettledPartDocument([.. part.Buildables.Select(id => id.Id)], part.Sweeps))],
            into);
    }

    public string ToJson() => JsonSerializer.Serialize(this, SimulationJson.Options);

    public static AnalysisDocument FromJson(string json) =>
        JsonSerializer.Deserialize<AnalysisDocument>(json, SimulationJson.Options) ?? throw new JsonException("An analysis document can't be empty");

    /// <summary>The buildables in a part of the world that didn't settle: their findings are approximate (BR-A10).</summary>
    public HashSet<string> Approximate() => [.. UnsettledParts.SelectMany(part => part.Buildables)];
}

public sealed record MachineStateDocument(string Id, double Utilisation, MachineLimit Limit, string? LimitingItem = null);

public sealed record BottleneckDocument(string Id, double Capacity, double Flow, double OfferedMore);

public sealed record UnusedSupplyDocument(string Id, string Item, double Offered, double Delivered);

public sealed record UnknownSupplyDocument(string Id, IReadOnlyDictionary<string, double> Delivered, double? StockMinutes, bool Confirmed);

public sealed record UnsettledPartDocument(IReadOnlyList<string> Buildables, int Sweeps);
