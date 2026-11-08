namespace SatisfactoryLedger.World.Snapshots;

/// <summary>A drone's route, kept on its home port (BR-A16).</summary>
/// <param name="Paired">The id of the port it flies to.</param>
/// <param name="DistanceMetres">To that port, measured flat.</param>
/// <param name="LatestTripSeconds">How long its latest round trip took; null when it hasn't made one yet.</param>
internal sealed record DroneDocument(string Paired, decimal DistanceMetres, decimal? LatestTripSeconds);