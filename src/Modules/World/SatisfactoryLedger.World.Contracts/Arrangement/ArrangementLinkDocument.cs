namespace SatisfactoryLedger.World.Contracts.Arrangement;

/// <param name="Belts">The belts (or pipes) it needs.</param>
public sealed record ArrangementLinkDocument(string From, string To, int Belts);
