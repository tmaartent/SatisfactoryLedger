namespace SatisfactoryLedger.World.Arrangement;

/// <param name="Name">Its name now; null when it was deleted since.</param>
/// <param name="Proposed">Where the run would place it; null until it's ready.</param>
internal sealed record ArrangedFactoryResponse(Guid FactoryId, string? Name, WorldRectangle Now, WorldRectangle? Proposed);
