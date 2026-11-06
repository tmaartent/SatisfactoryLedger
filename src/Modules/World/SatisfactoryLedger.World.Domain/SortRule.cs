namespace SatisfactoryLedger.World.Domain;

/// <summary>What a smart or programmable splitter sends out of one of its outputs (BR-A4).</summary>
/// <param name="Output">The output, counted from 0, as the splitter's settings number them.</param>
/// <param name="Item">The item, for <see cref="SortRuleKind.Item"/>; null otherwise.</param>
public sealed record SortRule(int Output, SortRuleKind Kind, ItemId? Item = null);

public enum SortRuleKind
{
    /// <summary>Any item.</summary>
    Any,

    /// <summary>Any item that no other output names.</summary>
    AnyUndefined,

    /// <summary>One item: <see cref="SortRule.Item"/>.</summary>
    Item,

    /// <summary>What the other outputs can't take.</summary>
    Overflow,

    /// <summary>Nothing.</summary>
    None,
}
