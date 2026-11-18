namespace SatisfactoryLedger.World.Domain.Arrangement;

/// <summary>An extractor feeding a factory one item: where it stands, and what it brings per minute.</summary>
public sealed record Supply(string Id, double X, double Y, double PerMinute);
