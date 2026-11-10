using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.Contracts.Analysis;
using SatisfactoryLedger.World.Snapshots;
namespace SatisfactoryLedger.World.Analysis;

/// <summary>A generator burning a known fuel (BR-A15): how fast it runs, and why not faster when below full power.</summary>
/// <param name="Fuel">The fuel it burns: the one in it at save time.</param>
/// <param name="Utilisation">Between 0 and 1, to 4 decimals.</param>
/// <param name="LimitingItem">The fuel or water it's starved of, or the waste it's blocked on; null when it runs.</param>
/// <param name="OnStock">The fuel it burns from what it holds, as for a machine (BR-A17); null when it doesn't.</param>
internal sealed record GeneratorFinding(
    string Id,
    string ClassName,
    string Fuel,
    double Utilisation,
    MachineLimit Limit,
    string? LimitingItem,
    PositionDocument? Position,
    bool Approximate,
    IReadOnlyDictionary<string, double> StarvedOf,
    IReadOnlyDictionary<string, double> BlockedOn,
    string? OnStock = null,
    double? StockMinutes = null);