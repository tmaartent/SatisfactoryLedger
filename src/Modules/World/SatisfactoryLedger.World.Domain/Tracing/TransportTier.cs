namespace SatisfactoryLedger.World.Domain.Tracing;

/// <summary>A tier of belt, lift or pipe the game has, as the Catalog knows it: its class name, kind and capacity per minute.</summary>
public sealed record TransportTier(string Id, LogisticsKind Kind, double Capacity);
