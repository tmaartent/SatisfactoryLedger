namespace SatisfactoryLedger.World.Domain;

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