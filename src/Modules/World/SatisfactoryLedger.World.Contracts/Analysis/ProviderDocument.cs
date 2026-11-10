namespace SatisfactoryLedger.World.Contracts.Analysis;

public sealed record ProviderDocument(string Id, string Item, double Offer) : FlowNodeDocument(Id);