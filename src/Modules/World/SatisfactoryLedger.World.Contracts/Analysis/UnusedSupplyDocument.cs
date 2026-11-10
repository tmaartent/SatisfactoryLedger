namespace SatisfactoryLedger.World.Contracts.Analysis;

public sealed record UnusedSupplyDocument(string Id, string Item, double Offered, double Delivered);