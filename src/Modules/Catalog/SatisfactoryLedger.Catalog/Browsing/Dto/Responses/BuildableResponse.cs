namespace SatisfactoryLedger.Catalog.Browsing.Dto.Responses;

/// <param name="Id">Its class name: <c>Build_TrainDockingStation_C</c>.</param>
/// <param name="Name">The name the game shows: "Freight Platform".</param>
internal sealed record BuildableResponse(string Id, string Name, bool InGame);
