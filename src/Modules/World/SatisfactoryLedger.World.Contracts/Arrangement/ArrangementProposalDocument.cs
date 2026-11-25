using System.Text.Json;

using SatisfactoryLedger.World.Contracts.Analysis;

namespace SatisfactoryLedger.World.Contracts.Arrangement;

/// <summary>A computed arrangement, as the analysis service sends it back.</summary>
/// <param name="Places">Per factory id, its proposed place on the grid.</param>
/// <param name="Cost">What the proposed layout costs: per link, belts times cells between the centres.</param>
/// <param name="CostNow">What the layout as built costs.</param>
/// <param name="Duration">How long the solver took.</param>
/// <param name="Belts">The belts between the placed factories, routed on the floor below them; null without a floor.</param>
/// <param name="Unrouted">Per link, the belts that couldn't be routed, not even with last-resort crossings.</param>
/// <param name="Placements">How many placements were tried (steps 101 and 102): another when belts were left unrouted or
/// needed last-resort crossings, keeping the one that left the fewest, then needed the fewest. 1 for a proposal from
/// before.</param>
public sealed record ArrangementProposalDocument(
    IReadOnlyDictionary<string, GridRectangleDocument> Places,
    double Cost,
    double CostNow,
    ArrangementQuality Quality,
    TimeSpan Duration,
    IReadOnlyList<RoutedBeltDocument>? Belts = null,
    IReadOnlyList<ArrangementLinkDocument>? Unrouted = null,
    int Placements = 1)
{
    public string ToJson() => JsonSerializer.Serialize(this, SimulationJson.Options);

    public static ArrangementProposalDocument FromJson(string json) =>
        JsonSerializer.Deserialize<ArrangementProposalDocument>(json, SimulationJson.Options)
        ?? throw new JsonException("An arrangement proposal document can't be empty");
}
