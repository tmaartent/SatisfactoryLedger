namespace SatisfactoryLedger.World.Domain;

/// <summary>Where some of what a factory makes of an item goes, per minute (BR-A18).</summary>
/// <param name="Owner">For <see cref="OutputDestinationKind.Factory"/>: the receiving factory.</param>
/// <param name="Building">For <see cref="OutputDestinationKind.Consumer"/>: the class of the building, such as <c>Build_Portal_C</c>.</param>
public sealed record OutgoingDestination(OutputDestinationKind Kind, Guid? Owner, string? Building, double PerMinute);
