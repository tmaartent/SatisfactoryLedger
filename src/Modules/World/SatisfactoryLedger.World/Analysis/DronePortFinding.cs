using SatisfactoryLedger.World.Snapshots;
namespace SatisfactoryLedger.World.Analysis;

/// <summary>
/// A drone port with fuel in its slot where drones refuel, or where none does (issue #44), what they burn there, and
/// what fuel reaches it (BR-A16). Rates per minute.
/// </summary>
/// <param name="Fuel">The fuel in its slot at save time.</param>
/// <param name="Burns">What the drones refuelling there burn, from their latest trips; null where no drone refuels.</param>
/// <param name="Supplied">What its belts bring of that fuel.</param>
/// <param name="NoDroneRefuels">No drone refuels there: it burns nothing, whatever is in its slot.</param>
internal sealed record DronePortFinding(
    string Id,
    string ClassName,
    string Fuel,
    double? Burns,
    double Supplied,
    PositionDocument? Position,
    bool Approximate,
    bool NoDroneRefuels);