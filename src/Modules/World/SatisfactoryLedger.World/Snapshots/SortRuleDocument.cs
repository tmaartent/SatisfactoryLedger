using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.Snapshots;

/// <param name="Item">The item's class name, for a rule of kind <c>item</c>.</param>
internal sealed record SortRuleDocument(int Output, SortRuleKind Kind, string? Item = null);