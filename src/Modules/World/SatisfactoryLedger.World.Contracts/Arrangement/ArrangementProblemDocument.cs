using System.Text.Json;

using SatisfactoryLedger.World.Contracts.Analysis;

namespace SatisfactoryLedger.World.Contracts.Arrangement;

/// <summary>What the analysis service needs to compute an arrangement: the problem, and how long it may take.</summary>
/// <param name="Together">How strongly the factories are kept together; 0 in runs from before it existed.</param>
public sealed record ArrangementProblemDocument(
    IReadOnlyList<ArrangedFactoryDocument> Factories,
    IReadOnlyList<FixedPlaceDocument> Fixed,
    IReadOnlyList<ArrangementLinkDocument> Links,
    GridRectangleDocument Area,
    int GapX,
    int GapY,
    int Together,
    int Seconds)
{
    public string ToJson() => JsonSerializer.Serialize(this, SimulationJson.Options);

    public static ArrangementProblemDocument FromJson(string json) =>
        JsonSerializer.Deserialize<ArrangementProblemDocument>(json, SimulationJson.Options)
        ?? throw new JsonException("An arrangement problem document can't be empty");
}
