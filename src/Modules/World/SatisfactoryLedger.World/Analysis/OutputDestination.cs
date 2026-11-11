namespace SatisfactoryLedger.World.Analysis;

/// <summary>Where some of a factory's output goes, per minute (BR-A18).</summary>
/// <param name="FactoryId">For <see cref="OutputDestinationKind.Factory"/>: the receiving factory.</param>
/// <param name="Factory">For <see cref="OutputDestinationKind.Factory"/>: its name.</param>
/// <param name="Building">For <see cref="OutputDestinationKind.Consumer"/>: the class of the building, such as <c>Build_Portal_C</c>.</param>
internal sealed record OutputDestination(OutputDestinationKind Kind, Guid? FactoryId, string? Factory, string? Building, double PerMinute);
