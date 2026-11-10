namespace SatisfactoryLedger.World.Contracts.Analysis;

public sealed record SortRuleDocument(int Output, SortRuleKind Kind, string? Item = null);