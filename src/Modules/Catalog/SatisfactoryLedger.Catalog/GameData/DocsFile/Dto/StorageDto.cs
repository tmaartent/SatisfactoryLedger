using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.GameData.DocsFile.Dto;

/// <summary>
/// A building with an inventory, as the Docs file sizes it (BR-A7). Each section names its size differently: a storage
/// container <c>mInventorySizeX</c> × <c>Y</c>, a freight platform <c>mStorageSizeX</c> × <c>Y</c>, a truck station
/// <c>mStorageInventorySize</c>, a fluid buffer <c>mStorageCapacity</c> in m³.
/// </summary>
internal class StorageDto
{
    [JsonPropertyName("ClassName")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("mDisplayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("mInventorySizeX")]
    public string InventorySizeX { get; set; } = "0";

    [JsonPropertyName("mInventorySizeY")]
    public string InventorySizeY { get; set; } = "0";

    [JsonPropertyName("mStorageSizeX")]
    public string StorageSizeX { get; set; } = "0";

    [JsonPropertyName("mStorageSizeY")]
    public string StorageSizeY { get; set; } = "0";

    [JsonPropertyName("mStorageInventorySize")]
    public string StorageInventorySize { get; set; } = "0";

    /// <summary>A fluid buffer's volume in m³, e.g. "2400.000000".</summary>
    [JsonPropertyName("mStorageCapacity")]
    public string StorageCapacity { get; set; } = "0";

    /// <summary>"True" for a truck station that holds fluid.</summary>
    [JsonPropertyName("mIsFluidStorageInventory")]
    public string IsFluidStorageInventory { get; set; } = "False";

    /// <summary>How many "stacks" of fluid one slot holds: 64 on the fluid freight platform, 1 elsewhere.</summary>
    [JsonPropertyName("mFluidStackSizeMultiplier")]
    public string FluidStackSizeMultiplier { get; set; } = "1";
}
