using System.Text.Json;

namespace SatisfactoryLedger.World.Contracts.Analysis;

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