namespace SatisfactoryLedger.World.Contracts.Analysis;

/// <param name="Stocked">The inputs whose stock inside it lasts (BR-A17); null when none.</param>
public sealed record MachineDocument(
    string Id, IReadOnlyDictionary<string, double> Demand, IReadOnlyDictionary<string, double> Production, IReadOnlyList<string>? Stocked = null) : FlowNodeDocument(Id);