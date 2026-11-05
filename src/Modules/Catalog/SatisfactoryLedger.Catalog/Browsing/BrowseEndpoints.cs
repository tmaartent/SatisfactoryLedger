using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

using SatisfactoryLedger.Catalog.Domain;
using SatisfactoryLedger.Catalog.Persistence;
using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.Catalog.Browsing;

internal sealed record ItemResponse(string Id, string Name, ItemForm Form, bool IsRawResource, bool InGame);

internal sealed record TransportTierResponse(string Id, string Name, TransportForm Form, int Capacity, bool InGame);

/// <summary>A rate in both shapes (chapter 05, "Representation"): a decimal for display, the exact fraction for calculations.</summary>
internal sealed record RateResponse(decimal PerMinute, string Exact)
{
    public static RateResponse From(Rate rate) => new(rate.ToDecimal(), rate.ToExactString());
}

internal sealed record ExtractorResponse(
    string Id,
    string Name,
    ItemForm Form,
    decimal AmountPerCycle,
    decimal CycleSeconds,
    RateResponse ExtractionRate,
    bool InGame)
{
    public static ExtractorResponse From(Extractor extractor) => new(
        extractor.Id.Id,
        extractor.Name,
        extractor.Form,
        extractor.AmountPerCycle,
        extractor.CycleSeconds,
        RateResponse.From(extractor.ExtractionRate),
        extractor.IsInGame);
}

internal sealed record PositionResponse(double X, double Y, double Z);

internal sealed record ResourceNodeResponse(
    string Name, ResourceNodeKind Kind, string Resource, Purity Purity, PositionResponse Position, bool InGame);

internal sealed record IngredientResponse(string ItemId, decimal AmountPerCycle, RateResponse Rate);

internal sealed record RecipeResponse(
    string Id,
    string Name,
    string BuildingId,
    decimal CycleSeconds,
    bool IsAlternate,
    bool InGame,
    IReadOnlyList<IngredientResponse> Inputs,
    IReadOnlyList<IngredientResponse> Outputs)
{
    public static RecipeResponse From(Recipe recipe) => new(
        recipe.Id.Id,
        recipe.Name,
        recipe.BuildingId.Id,
        recipe.CycleSeconds,
        recipe.IsAlternate,
        recipe.IsInGame,
        ToResponses(recipe.Inputs, recipe.BaseInputRates()),
        ToResponses(recipe.Outputs, recipe.BaseOutputRates()));

    private static IngredientResponse[] ToResponses(IReadOnlyList<RecipeIngredient> ingredients, ItemRate[] rates) =>
    [
        .. ingredients.Zip(rates, (ingredient, rate) =>
            new IngredientResponse(ingredient.ItemId.Id, ingredient.Amount, RateResponse.From(rate.Rate)))
    ];
}

/// <summary>
/// Read-only queries. They skip the use-case layer and read straight from the DbContext (chapter 04, light CQRS).
/// Entries that are no longer in the game are included, flagged with inGame = false.
/// </summary>
internal static class BrowseEndpoints
{
    public static RouteGroupBuilder MapBrowseEndpoints(this RouteGroupBuilder catalog)
    {
        catalog.MapGet("/items", GetItemsAsync)
            .WithName("GetItems")
            .WithSummary("Items, optionally only one form: ?form=solid or ?form=fluid");

        catalog.MapGet("/recipes", GetRecipesAsync)
            .WithName("GetRecipes")
            .WithSummary("Recipes with their per-minute rates, optionally only those that produce an item (alternates included)");

        catalog.MapGet("/transport-tiers", GetTransportTiersAsync)
            .WithName("GetTransportTiers")
            .WithSummary("Belt, lift and pipe tiers with their capacity per minute");

        catalog.MapGet("/resource-nodes", GetResourceNodesAsync)
            .WithName("GetResourceNodes")
            .WithSummary("Resource nodes and resource well satellites from the map, optionally only one resource: ?resource=Desc_OreIron_C");

        catalog.MapGet("/extractors", GetExtractorsAsync)
            .WithName("GetExtractors")
            .WithSummary("Miners and extractors with what they extract per minute on a normal-purity node at 100% clock speed");

        return catalog;
    }

    // `form` is a string, not an ItemForm: minimal APIs bind enums case-sensitively ("Fluid"),
    // but the API writes them as "fluid", so it has to read them that way too.
    private static async Task<Results<Ok<List<ItemResponse>>, ValidationProblem>> GetItemsAsync(
        string? form, CatalogDbContext db, CancellationToken cancellationToken)
    {
        IQueryable<Item> items = db.Items.AsNoTracking();
        if (form is not null)
        {
            if (!Enum.TryParse(form, ignoreCase: true, out ItemForm itemForm) || !Enum.IsDefined(itemForm))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["form"] = [$"Use one of: {string.Join(", ", Enum.GetNames<ItemForm>().Select(name => name.ToLowerInvariant()))}."]
                });
            }

            items = items.Where(item => item.ItemForm == itemForm);
        }

        List<ItemResponse> response = await items
            .OrderBy(item => item.Name)
            .Select(item => new ItemResponse(item.Id.Id, item.Name, item.ItemForm, item.IsRawResource, item.IsInGame))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(response);
    }

    private static async Task<Ok<List<RecipeResponse>>> GetRecipesAsync(
        string? produces, CatalogDbContext db, CancellationToken cancellationToken)
    {
        IQueryable<Recipe> recipes = db.Recipes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(produces))
        {
            ItemId product = ItemId.Of(produces);
            // Queries go through the mapped field, because the public Outputs property isn't mapped (see RecipeConfiguration).
            recipes = recipes.Where(recipe => EF.Property<List<RecipeIngredient>>(recipe, RecipeConfiguration.OutputsField)
                .Any(output => output.ItemId == product));
        }

        List<Recipe> found = await recipes.OrderBy(recipe => recipe.Name).ToListAsync(cancellationToken);

        // Rates are computed by the domain (Recipe.BaseInputRates), so this mapping runs in memory, not in SQL.
        return TypedResults.Ok(found.Select(RecipeResponse.From).ToList());
    }

    private static async Task<Ok<List<TransportTierResponse>>> GetTransportTiersAsync(
        CatalogDbContext db, CancellationToken cancellationToken)
    {
        List<TransportTierResponse> response = await db.TransportTiers.AsNoTracking()
            .OrderBy(tier => tier.Form).ThenBy(tier => tier.Capacity).ThenBy(tier => tier.Name)
            .Select(tier => new TransportTierResponse(tier.Id.Id, tier.Name, tier.Form, tier.Capacity, tier.IsInGame))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(response);
    }

    private static async Task<Ok<List<ExtractorResponse>>> GetExtractorsAsync(
        CatalogDbContext db, CancellationToken cancellationToken)
    {
        List<Extractor> extractors = await db.Extractors.AsNoTracking()
            .OrderBy(extractor => extractor.Name)
            .ToListAsync(cancellationToken);

        // The rate is computed by the domain (Extractor.ExtractionRate), so this mapping runs in memory, like recipes.
        return TypedResults.Ok(extractors.Select(ExtractorResponse.From).ToList());
    }

    private static async Task<Ok<List<ResourceNodeResponse>>> GetResourceNodesAsync(
        string? resource, CatalogDbContext db, CancellationToken cancellationToken)
    {
        IQueryable<ResourceNode> nodes = db.ResourceNodes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(resource))
        {
            ItemId item = ItemId.Of(resource);
            nodes = nodes.Where(node => node.Resource == item);
        }

        List<ResourceNodeResponse> response = await nodes
            .OrderBy(node => node.Id)
            .Select(node => new ResourceNodeResponse(
                node.Id.Id, node.Kind, node.Resource.Id, node.Purity,
                new PositionResponse(node.Position.X, node.Position.Y, node.Position.Z), node.IsInGame))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(response);
    }
}
