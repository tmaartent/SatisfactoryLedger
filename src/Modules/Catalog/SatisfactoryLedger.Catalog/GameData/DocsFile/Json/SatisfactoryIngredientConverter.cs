using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using SatisfactoryLedger.Catalog.GameData.DocsFile.Dto;

namespace SatisfactoryLedger.Catalog.GameData.DocsFile.Json;

internal partial class SatisfactoryIngredientConverter : JsonConverter<List<RecipeIngredientDto>>
{
    private static readonly Regex IngredientRegex = ItemClassRegex();
    
    public override List<RecipeIngredientDto> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var ingredientsList = new List<RecipeIngredientDto>();
        string? rawString = reader.GetString();

        if (string.IsNullOrWhiteSpace(rawString))
        {
            return ingredientsList;
        }

        MatchCollection matches = IngredientRegex.Matches(rawString);
        foreach (Match match in matches)
        {
            if (match.Success)
            {
                ingredientsList.Add(new RecipeIngredientDto
                {
                    ClassName = match.Groups["className"].Value,
                    Amount = int.Parse(match.Groups["amount"].Value, NumberFormatInfo.InvariantInfo)
                });
            }
        }

        return ingredientsList;
    }

    public override void Write(Utf8JsonWriter writer, List<RecipeIngredientDto> value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    [GeneratedRegex("""ItemClass=".*?\.(?<className>[a-zA-Z0-9_]+)'",\s*Amount=(?<amount>\d+)""", RegexOptions.Compiled
    )]
    private static partial Regex ItemClassRegex();
}