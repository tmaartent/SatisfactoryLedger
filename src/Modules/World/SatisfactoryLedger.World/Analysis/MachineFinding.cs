using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.Snapshots;
namespace SatisfactoryLedger.World.Analysis;

/// <summary>A machine, how fast it runs, and why not faster when below 100% (BR-A11).</summary>
/// <param name="Utilisation">Between 0 and 1, to 4 decimals.</param>
/// <param name="LimitingItem">The item it's starved of or blocked on; null when it runs or has no known recipe.</param>
/// <param name="Approximate">True when its part of the world didn't settle (BR-A10).</param>
/// <param name="OnStock">
/// The input it runs on from what it holds, because its belts bring less than it uses (BR-A17); null when it doesn't.
/// </param>
/// <param name="StockMinutes">How long that stock lasts at the rate it's used up; null when it doesn't run on stock.</param>
/// <param name="StarvedOf">Every input it's offered less of than it needs at 100%, with the share it's offered; the worst first.</param>
/// <param name="BlockedOn">Every product that can't all leave, with the share that can; the worst first.</param>
internal sealed record MachineFinding(
    string Id,
    string ClassName,
    string? Recipe,
    double Utilisation,
    MachineLimit Limit,
    string? LimitingItem,
    PositionDocument? Position,
    bool Approximate,
    IReadOnlyDictionary<string, double> StarvedOf,
    IReadOnlyDictionary<string, double> BlockedOn,
    string? OnStock = null,
    double? StockMinutes = null);