using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.Importer;

internal class ItemDto
{
    [JsonPropertyName("ClassName")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("mDisplayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("mDescription")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("mStackSize")]
    public string StackSize { get; set; } = string.Empty;
    
    [JsonPropertyName("mForm")]
    public string Form { get; set; } = string.Empty;

    [JsonPropertyName("mResourceSinkPoints")]
    public string ResourceSinkPoints { get; set; } = string.Empty;

    [JsonPropertyName("mCachedStackSize")]
    public string CachedStackSize { get; set; } = string.Empty;
    
    [JsonIgnore]
    public bool IsFluid {
        get
        {
            return Form is "RF_LIQUID" or "RF_GAS";
        } 
    }
    
}