using SatisfactoryLedger.World.Domain.Flow.Objects;

namespace SatisfactoryLedger.World.Domain.Tracing;

/// <summary>
/// How the analysis found a machine or generator running (BR-A11): its limit and utilisation, and per item the share it
/// gets of an input it's short of, or the share that can leave of a product that can't all leave.
/// </summary>
public sealed record MachineReading(
    MachineLimit Limit,
    double Utilisation,
    IReadOnlyDictionary<ItemId, double> StarvedOf,
    IReadOnlyDictionary<ItemId, double> BlockedOn);
