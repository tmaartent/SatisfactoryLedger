using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;

namespace SatisfactoryLedger.World.Simulation;

/// <param name="BurnRate">For a drone port: its fuel per minute; null when that isn't known (BR-A16).</param>
public sealed record UnknownSourceDocument(
    string Id, IReadOnlyDictionary<string, double> Stock, Carries Carries, IReadOnlyList<string>? Burns = null, double? BurnRate = null)
    : FlowNodeDocument(Id)
{
    public override FlowNode ToNode() => new FlowUnknownSource(BuildableId.Of(Id), Items(Stock), Carries, Items(Burns), BurnRate);
}