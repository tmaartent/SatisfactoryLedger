using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.Importer;

internal class GeneratorFuelDto
{
    [JsonPropertyName("mFuelClass")]
    public string FuelClass { get; set; } = string.Empty;

    /// <summary>Empty when it needs none.</summary>
    [JsonPropertyName("mSupplementalResourceClass")]
    public string SupplementalResourceClass { get; set; } = string.Empty;

    /// <summary>Empty when the fuel leaves nothing.</summary>
    [JsonPropertyName("mByproduct")]
    public string Byproduct { get; set; } = string.Empty;

    [JsonPropertyName("mByproductAmount")]
    public string ByproductAmount { get; set; } = string.Empty;
}