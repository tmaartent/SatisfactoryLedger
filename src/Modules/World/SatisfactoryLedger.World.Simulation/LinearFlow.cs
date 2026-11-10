using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow.Objects;

namespace SatisfactoryLedger.World.Simulation;

/// <summary>A solved linear programme (ADR-0026): what flows over each connection, and how fast each machine runs.</summary>
/// <param name="Flows">Per connection of <paramref name="Network"/>, in its order: per item, what flows over it a minute.</param>
/// <param name="Utilisation">Per machine, between 0 and 1 (BR-A1). A machine without a recipe runs at 0.</param>
internal sealed record LinearFlow(
    FlowNetwork Network,
    IReadOnlyList<IReadOnlyDictionary<ItemId, double>> Flows,
    IReadOnlyDictionary<BuildableId, double> Utilisation);
