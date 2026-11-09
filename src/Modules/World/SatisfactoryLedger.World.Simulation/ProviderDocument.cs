using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.Domain.Flow.Objects;

namespace SatisfactoryLedger.World.Simulation;

public sealed record ProviderDocument(string Id, string Item, double Offer) : FlowNodeDocument(Id)
{
    public override FlowNode ToNode() => new FlowProvider(BuildableId.Of(Id), ItemId.Of(Item), Offer);
}