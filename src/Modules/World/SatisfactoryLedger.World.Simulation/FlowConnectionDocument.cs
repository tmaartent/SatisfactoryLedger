namespace SatisfactoryLedger.World.Simulation;

public sealed record FlowConnectionDocument(string From, string To, int? FromPort = null, int? ToPort = null);