using SatisfactoryLedger.World.Snapshots;
namespace SatisfactoryLedger.World.Analysis;

/// <summary>A drone port where drones refuel, what they burn there, and what fuel reaches it (BR-A16). Rates per minute.</summary>
/// <param name="Fuel">The fuel in its slot at save time.</param>
/// <param name="Burns">What the drones refuelling there burn, from their latest trips.</param>
/// <param name="Supplied">What its belts bring of that fuel.</param>
internal sealed record DronePortFinding(
    string Id,
    string ClassName,
    string Fuel,
    double Burns,
    double Supplied,
    PositionDocument? Position,
    bool Approximate);