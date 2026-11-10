namespace SatisfactoryLedger.World.Contracts.Analysis;

/// <summary>What one output of a smart or programmable splitter sends (BR-A4): as the domain's <c>SortRuleKind</c>, written by name.</summary>
public enum SortRuleKind
{
    Any,
    AnyUndefined,
    Item,
    Overflow,
    None,
}