using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.SaveFile;

/// <summary>A drone's route from what the save says about its home port (BR-A16).</summary>
internal static class DroneRoutes
{
    /// <summary>
    /// The route between a home port and the port it's paired with. The distance is measured flat, as the game does: a
    /// trip's cost in the save matches 24,000 MJ + 6 MJ × twice the flat distance, to the megajoule. Null when the port
    /// has no paired port.
    /// </summary>
    /// <param name="tripSeconds">
    /// The durations of its latest trips; the game keeps one (<c>mTripInformationSampleCount</c>). None when its drone
    /// hasn't made a trip yet: the route then has no trip time.
    /// </param>
    public static SavedDroneRoute? Of(SavedPosition home, (BuildableId Id, SavedPosition Position)? paired, IReadOnlyCollection<float> tripSeconds)
    {
        if (paired is not { } other)
        {
            return null;
        }

        double dx = other.Position.X - home.X;
        double dy = other.Position.Y - home.Y;
        // Positions are in centimetres; rounded to the centimetre and to a hundredth of a second, the save's own precision.
        decimal metres = Math.Round((decimal)Math.Sqrt(dx * dx + dy * dy) / 100, 2);
        decimal? seconds = tripSeconds.Count == 0 ? null : Math.Round(tripSeconds.Select(trip => (decimal)trip).Average(), 2);
        return new SavedDroneRoute(other.Id, metres, seconds > 0 ? seconds : null);
    }
}
