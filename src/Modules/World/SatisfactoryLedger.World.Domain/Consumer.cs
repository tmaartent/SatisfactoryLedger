namespace SatisfactoryLedger.World.Domain;

/// <summary>
/// Takes items out of the network: an AWESOME Sink, a dimensional depot uploader, a power generator, or a station
/// that loads. Never part of a factory (BR-W8).
/// </summary>
public sealed class Consumer(BuildableId id) : Buildable(id);
