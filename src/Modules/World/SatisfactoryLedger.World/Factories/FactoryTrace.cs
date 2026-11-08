namespace SatisfactoryLedger.World.Factories;

/// <param name="Traceable">False when the factory's output belt isn't in this snapshot (BR-W11).</param>
/// <param name="Logistics">How many belts, splitters and other logistics belong to the factory.</param>
internal sealed record FactoryTrace(Guid FactoryId, string Name, bool Traceable, IReadOnlyList<MachineSummary> Machines, int Logistics);