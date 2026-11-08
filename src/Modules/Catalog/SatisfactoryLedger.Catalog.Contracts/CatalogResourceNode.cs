namespace SatisfactoryLedger.Catalog.Contracts;

/// <param name="Name">The node's name, as the save uses it: <c>BP_ResourceNode35</c>.</param>
/// <param name="Kind"><c>resourceNode</c> or <c>wellSatellite</c>.</param>
/// <param name="Resource">The item's class name: <c>Desc_OreIron_C</c>.</param>
/// <param name="Purity"><c>impure</c>, <c>normal</c> or <c>pure</c> (BR-W9).</param>
public sealed record CatalogResourceNode(string Name, string Kind, string Resource, string Purity);