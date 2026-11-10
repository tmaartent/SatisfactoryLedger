using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.Snapshots;

/// <summary>The Catalog copy of a snapshot, by id, to look up while building the world.</summary>
internal sealed record CopiedCatalog(
    IReadOnlyDictionary<string, RecipeDocument> Recipes,
    IReadOnlyDictionary<string, ExtractorDocument> Extractors,
    IReadOnlyDictionary<string, TransportTierDocument> TransportTiers,
    IReadOnlyDictionary<string, ResourceNodeDocument> ResourceNodes,
    IReadOnlyDictionary<string, GeneratorDocument> Generators)
{
    /// <summary>What each building class with an inventory holds (BR-A7).</summary>
    public IReadOnlyDictionary<string, StorageDocument> Storages { get; init; } = new Dictionary<string, StorageDocument>();

    /// <summary>How many of each item held anywhere fit in a slot (BR-A7).</summary>
    public IReadOnlyDictionary<ItemId, int> StackSizes { get; init; } = new Dictionary<ItemId, int>();

    /// <summary>The dimensional depot's inventory, for every uploader; null when the snapshot says nothing about it.</summary>
    public Inventory? Depot { get; init; }
}