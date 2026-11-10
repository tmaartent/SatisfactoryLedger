namespace SatisfactoryLedger.World.Contracts.Analysis;

public sealed record FlowConnectionDocument(string From, string To, int? FromPort = null, int? ToPort = null);