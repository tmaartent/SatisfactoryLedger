using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.GameData.DocsFile.Dto;

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
    
    /// <summary>Megajoules in one item, or in one litre of a fluid: what a generator gets from burning it. "0.000000" for non-fuels.</summary>
    [JsonPropertyName("mEnergyValue")]
    public string EnergyValue { get; set; } = "0";

    [JsonPropertyName("mForm")]
    public string Form { get; set; } = string.Empty;

    /// <summary>"(B=212,G=176,R=122,A=0)": blue, green, red and alpha, 0 to 255. White for a solid.</summary>
    [JsonPropertyName("mFluidColor")]
    public string FluidColor { get; set; } = string.Empty;

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