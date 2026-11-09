namespace SatisfactoryLedger.World.Factories;

/// <param name="Traceable">False when none of its machines is in this snapshot, or it hasn't been converted to its machines yet (BR-W11).</param>
/// <param name="Logistics">How many belts, splitters and other logistics belong to the factory (BR-W3).</param>
/// <param name="Missing">Its machines that aren't in this snapshot, not even rebuilt (BR-W11).</param>
internal sealed record FactoryTrace(
    Guid FactoryId,
    string Name,
    bool Traceable,
    IReadOnlyList<MachineSummary> Machines,
    int Logistics,
    IReadOnlyList<ConnectionSummary> Outputs,
    IReadOnlyList<ConnectionSummary> Inputs,
    IReadOnlyList<BuildableReference> Missing);
