using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.GameData.DocsFile;

internal class RecipeGroupDto: GameDataGroupDto
{
    [JsonPropertyName("Classes")]
    public List<RecipeDto> Recipes
    {
        get;
        set
        {
            field = value
                .Where(b => b.RecipeBuilding != null)
                .ToList() ?? [];
        }
    } = [];
}