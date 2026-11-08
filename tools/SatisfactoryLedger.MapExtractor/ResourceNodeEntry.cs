namespace SatisfactoryLedger.MapExtractor;

/// <param name="Name">The node's name, as the game and the save use it: <c>BP_ResourceNode35</c>.</param>
/// <param name="Resource">The item's class name: <c>Desc_OreIron_C</c>.</param>
internal sealed record ResourceNodeEntry(string Name, NodeKind Kind, string Resource, Purity Purity, NodePosition? Position);