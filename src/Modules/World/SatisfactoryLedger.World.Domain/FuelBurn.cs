using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.World.Domain;

/// <summary>
/// What a drone port burns of the fuel in its slot (BR-A16): one of three, never a rate of 0 for "nothing"
/// (decided 2026-11-26, issue #44).
/// </summary>
public abstract record FuelBurn
{
    private FuelBurn()
    {
    }

    /// <summary>The drones refuelling there burn this much per minute, from their latest trips.</summary>
    public sealed record Known(Rate PerMinute) : FuelBurn;

    /// <summary>
    /// Drones refuel there, but what they burn isn't known: one hasn't made a trip yet, or the fuel's energy isn't
    /// known. A truck station's fuel too. It takes its fuel only from what the line's other consumers leave (BR-A8).
    /// </summary>
    public sealed record NotKnown : FuelBurn;

    /// <summary>No drone refuels there: it burns nothing, whatever is in its slot.</summary>
    public sealed record NoDroneRefuels : FuelBurn;
}
