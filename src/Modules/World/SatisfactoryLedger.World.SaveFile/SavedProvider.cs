using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.SaveFile;

/// <param name="Node">What it extracts from: a resource node (<c>BP_ResourceNode35</c>) or a water volume.</param>
/// <param name="Resource">
/// The item it extracts, when its class decides that: a water extractor pumps water (<c>Desc_Water_C</c>) wherever it
/// stands. Null when the node decides, as for miners; the Catalog knows the nodes.
/// </param>
public sealed record SavedProvider(BuildableId Id, string ClassName, string? Node, ClockSpeed ClockSpeed, string? Resource = null)
    : SavedBuildable(Id, ClassName);