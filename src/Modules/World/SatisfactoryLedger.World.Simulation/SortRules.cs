using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow.Objects;

namespace SatisfactoryLedger.World.Simulation;

/// <summary>
/// BR-A4: where a smart or programmable splitter sends an item. Each output's rules name the item, <i>any</i>, or
/// <i>any undefined</i> (an item no output names); an <i>overflow</i> output gets only what the others can't take, and
/// an output set to <i>none</i> nothing. An output whose port isn't known gets nothing.
/// </summary>
internal sealed class SortRules
{
    private readonly FlowNetwork _network;
    private readonly FlowSplitter _splitter;
    private readonly HashSet<ItemId> _named;

    public SortRules(FlowNetwork network, FlowSplitter splitter)
    {
        _network = network;
        _splitter = splitter;
        _named = [.. (splitter.Rules ?? []).Where(rule => rule.Kind == SortRuleKind.Item).Select(rule => rule.Item!)];
    }

    /// <summary>The output a connection leaves from sends this item as one of the splitter's main outputs.</summary>
    public bool Takes(int edge, ItemId item) => RulesAt(edge).Any(rule => rule.Kind switch
    {
        SortRuleKind.Any => true,
        SortRuleKind.Item => rule.Item == item,
        SortRuleKind.AnyUndefined => !_named.Contains(item),
        _ => false,
    });

    /// <summary>The output a connection leaves from gets this item only when the main outputs can't take it.</summary>
    public bool Overflows(int edge, ItemId item) => !Takes(edge, item) && RulesAt(edge).Any(rule => rule.Kind == SortRuleKind.Overflow);

    private IEnumerable<SortRule> RulesAt(int edge) =>
        _network.Ports[edge].From is { } port ? (_splitter.Rules ?? []).Where(rule => rule.Output == port) : [];
}
