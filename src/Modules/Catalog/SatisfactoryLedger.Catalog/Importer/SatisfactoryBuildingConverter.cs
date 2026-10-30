using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SatisfactoryLedger.Catalog.Importer;

internal partial class SatisfactoryBuildingConverter : JsonConverter<RecipeBuildingDto>
{
    private static readonly Regex BuildingRegex = BuildableFactoryRegex();
    
    public override RecipeBuildingDto? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? rawString = reader.GetString();

        if (string.IsNullOrWhiteSpace(rawString))
        {
            return null;
        }

        var match = BuildingRegex.Match(rawString);
        return match.Success ? new RecipeBuildingDto { ClassName = match.Groups["className"].Value } : null;
    }

    public override void Write(Utf8JsonWriter writer, RecipeBuildingDto value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
    
    [GeneratedRegex("""/Buildable/Factory/[^"]*?\.(?<className>Build_[a-zA-Z0-9_]+_C\b)""")]
    private static partial Regex BuildableFactoryRegex();
}