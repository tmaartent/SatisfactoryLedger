using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.SaveFile;

/// <summary>A drone's route (BR-A16): the port it's paired with, the flat distance to it, and how long its latest round trip took.</summary>
/// <param name="LatestTripSeconds">Null when its drone hasn't made a trip yet.</param>
public sealed record SavedDroneRoute(BuildableId Paired, decimal DistanceMetres, decimal? LatestTripSeconds);