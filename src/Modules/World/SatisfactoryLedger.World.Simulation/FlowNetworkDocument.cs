using System.Text.Json;

using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.Domain.Flow.Objects;

namespace SatisfactoryLedger.World.Simulation;

/// <summary>
/// A flow network as JSON: what World sends the analysis service (ADR-0018). Every node with its rates, every
/// connection with its ports, and which items are fluids.
/// </summary>
public sealed record FlowNetworkDocument(
    int Version,
    IReadOnlyList<FlowNodeDocument> Nodes,
    IReadOnlyList<FlowConnectionDocument> Connections,
    IReadOnlyList<string> Fluids)
{
    public const int CurrentVersion = 1;

    public static FlowNetworkDocument From(FlowNetwork network)
    {
        ArgumentNullException.ThrowIfNull(network);

        return new FlowNetworkDocument(
            CurrentVersion,
            [.. network.Nodes.Select(FlowNodeDocument.From)],
            [
                .. Enumerable.Range(0, network.Edges.Count).Select(edge => new FlowConnectionDocument(
                    network.Nodes[network.Edges[edge].From].Id.Id,
                    network.Nodes[network.Edges[edge].To].Id.Id,
                    network.Ports[edge].From,
                    network.Ports[edge].To))
            ],
            [.. network.Fluids.Select(item => item.Id).Order(StringComparer.Ordinal)]);
    }

    public FlowNetwork ToNetwork() => new(
        Nodes.Select(node => node.ToNode()),
        Connections.Select(connection => new Connection(
            BuildableId.Of(connection.From), BuildableId.Of(connection.To), connection.FromPort, connection.ToPort)),
        Fluids.Select(ItemId.Of));

    public string ToJson() => JsonSerializer.Serialize(this, SimulationJson.Options);

    public static FlowNetworkDocument FromJson(string json)
    {
        FlowNetworkDocument document = JsonSerializer.Deserialize<FlowNetworkDocument>(json, SimulationJson.Options)
            ?? throw new JsonException("A flow network document can't be empty");
        return document.Version == CurrentVersion
            ? document
            : throw new NotSupportedException($"Flow network document version {document.Version} is not supported");
    }
}