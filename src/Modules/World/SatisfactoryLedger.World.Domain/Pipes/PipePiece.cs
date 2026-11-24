namespace SatisfactoryLedger.World.Domain.Pipes;

/// <summary>A pipe, junction, pump, valve or fluid buffer of a pipe network, as the head lift sees it (BR-A22).</summary>
/// <param name="TopMetres">How high it reaches at its highest point. Fluid that reaches less high doesn't get across.</param>
/// <param name="Lift">For a pump whose outlet is known, the lift it gives; null for every other piece.</param>
/// <param name="CarriesGas">Whether its network carries a gas, which needs no head lift (asked 2026-11-24).</param>
public sealed record PipePiece(BuildableId Id, double TopMetres, PumpLift? Lift = null, bool CarriesGas = false)
{
    /// <summary>How far the reach of fluid is above its top: negative when its top is above the reach, short of head lift.</summary>
    public double Headroom(double reachMetres) => reachMetres - TopMetres;
}
