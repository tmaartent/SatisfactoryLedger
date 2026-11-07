using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.Importer;

/// <summary>
/// Generators come in two sections: nuclear, and the others that burn fuel (coal, fuel, biomass). STJ can register a
/// type for only one discriminator, so each section gets its own subclass. Geothermal burns nothing: not imported.
/// </summary>
internal abstract class GeneratorGroupDto : GameDataGroupDto
{
    [JsonPropertyName("Classes")]
    public List<GeneratorDto> Generators { get; set; } = [];
}

internal class FuelGeneratorGroupDto : GeneratorGroupDto;

internal class NuclearGeneratorGroupDto : GeneratorGroupDto;

internal class GeneratorDto
{
    [JsonPropertyName("ClassName")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("mDisplayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Megawatts at 100% clock speed, e.g. "2500.000000".</summary>
    [JsonPropertyName("mPowerProduction")]
    public string PowerProduction { get; set; } = string.Empty;

    /// <summary>Litres of the supplemental fluid per megajoule, e.g. "1.600000"; "0.000000" when it needs none.</summary>
    [JsonPropertyName("mSupplementalToPowerRatio")]
    public string SupplementalToPowerRatio { get; set; } = "0";

    [JsonPropertyName("mFuel")]
    public List<GeneratorFuelDto> Fuels { get; set; } = [];
}

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
