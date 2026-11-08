using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;

namespace SatisfactoryLedger.World.Simulation;

/// <param name="Capacity">Per minute; null when there's no limit.</param>
public sealed record ConduitDocument(string Id, double? Capacity, Carries Carries) : FlowNodeDocument(Id)
{
    public override FlowNode ToNode() => new FlowConduit(BuildableId.Of(Id), Capacity ?? double.PositiveInfinity, Carries);
}