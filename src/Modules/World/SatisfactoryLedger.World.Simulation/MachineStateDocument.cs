using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.Domain.Flow.Objects;

namespace SatisfactoryLedger.World.Simulation;

/// <param name="StarvedOf">Per input it's short of, the share it's offered; null in an analysis older than version 6.</param>
/// <param name="BlockedOn">Per product that can't all leave, the share that can; null in an analysis older than version 6.</param>
public sealed record MachineStateDocument(
    string Id,
    double Utilisation,
    MachineLimit Limit,
    string? LimitingItem = null,
    IReadOnlyDictionary<string, double>? StarvedOf = null,
    IReadOnlyDictionary<string, double>? BlockedOn = null);