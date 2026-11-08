using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.Simulation;

public sealed record SortRuleDocument(int Output, SortRuleKind Kind, string? Item = null);