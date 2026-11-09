using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.Domain.Flow.Objects;

namespace SatisfactoryLedger.World.Simulation;

/// <param name="Stocked">The inputs whose stock inside it lasts (BR-A17); null when none.</param>
public sealed record MachineDocument(
    string Id, IReadOnlyDictionary<string, double> Demand, IReadOnlyDictionary<string, double> Production, IReadOnlyList<string>? Stocked = null)
    : FlowNodeDocument(Id)
{
    public override FlowNode ToNode() => new FlowMachine(BuildableId.Of(Id), Items(Demand), Items(Production), Items(Stocked));
}