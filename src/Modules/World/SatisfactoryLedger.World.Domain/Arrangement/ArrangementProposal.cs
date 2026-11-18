namespace SatisfactoryLedger.World.Domain.Arrangement;

/// <summary>Where the factories would best be placed, and the belt work that saves (decided 2026-11-12).</summary>
/// <param name="Places">Per factory, its proposed place.</param>
/// <param name="Cost">What the proposed layout costs: per link, belts times cells between the centres.</param>
/// <param name="CostNow">What the layout as built costs.</param>
public sealed record ArrangementProposal(
    IReadOnlyDictionary<string, GridRectangle> Places, double Cost, double CostNow, ArrangementStatus Status);
