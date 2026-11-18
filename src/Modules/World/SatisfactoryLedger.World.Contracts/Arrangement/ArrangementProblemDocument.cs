using System.Text.Json;

using SatisfactoryLedger.World.Contracts.Analysis;

namespace SatisfactoryLedger.World.Contracts.Arrangement;

/// <summary>What the analysis service needs to compute an arrangement: the problem, and how long it may take.</summary>
/// <param name="Together">How strongly the factories are kept together; 0 in runs from before it existed.</param>
/// <param name="Floor">The floor below the factories to route their belts on; null in runs from before it existed.</param>
/// <param name="Spots">Where the nodes' extractors, stations and drone ports that links reach stand on the belt floor.</param>
public sealed record ArrangementProblemDocument(
    IReadOnlyList<ArrangedFactoryDocument> Factories,
    IReadOnlyList<FixedPlaceDocument> Fixed,
    IReadOnlyList<ArrangementLinkDocument> Links,
    GridRectangleDocument Area,
    int GapX,
    int GapY,
    int Together,
    int Seconds,
    BeltFloorDocument? Floor = null,
    IReadOnlyList<SpotDocument>? Spots = null)
{
    public string ToJson() => JsonSerializer.Serialize(this, SimulationJson.Options);

    public static ArrangementProblemDocument FromJson(string json) =>
        JsonSerializer.Deserialize<ArrangementProblemDocument>(json, SimulationJson.Options)
        ?? throw new JsonException("An arrangement problem document can't be empty");
}
