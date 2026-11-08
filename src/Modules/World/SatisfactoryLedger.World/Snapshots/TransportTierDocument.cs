namespace SatisfactoryLedger.World.Snapshots;

/// <param name="Id">The building's class name, e.g. <c>Build_ConveyorBeltMk1_C</c>.</param>
internal sealed record TransportTierDocument(string Id, int CapacityPerMinute);