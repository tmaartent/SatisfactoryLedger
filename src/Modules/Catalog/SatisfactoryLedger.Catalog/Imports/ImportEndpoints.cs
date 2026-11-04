using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

using SatisfactoryLedger.Catalog.Domain;
using SatisfactoryLedger.Catalog.Persistence;

namespace SatisfactoryLedger.Catalog.Imports;

internal sealed record ImportCountsResponse(int Added, int Updated, int NoLongerInGame);

internal sealed record ImportResponse(
    Guid Id,
    string GameVersion,
    DateTimeOffset ImportedAt,
    ImportCountsResponse Items,
    ImportCountsResponse Buildings,
    ImportCountsResponse TransportTiers,
    ImportCountsResponse Recipes,
    IReadOnlyList<string> NoLongerInGame)
{
    public static ImportResponse From(CatalogImport import) => new(
        import.Id.Id,
        import.GameVersion,
        import.ImportedAt,
        ToResponse(import.Items),
        ToResponse(import.Buildings),
        ToResponse(import.TransportTiers),
        ToResponse(import.Recipes),
        import.NoLongerInGame);

    private static ImportCountsResponse ToResponse(ImportCounts counts) =>
        new(counts.Added, counts.Updated, counts.NoLongerInGame);
}

internal static class ImportEndpoints
{
    public const string InvalidDocsFileCode = "Catalog.InvalidDocsFile";

    public static RouteGroupBuilder MapImportEndpoints(this RouteGroupBuilder catalog)
    {
        catalog.MapPost("/imports", ImportAsync)
            .WithName("ImportCatalog")
            .WithSummary("Import the game's Docs file (en-US.json)")
            .WithDescription("Adds new entries, updates existing ones and marks entries missing from the file as no longer in the game. Importing the same file again changes nothing.")
            // A ProblemHttpResult doesn't tell OpenAPI its status code, so the 422 is declared here.
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            // The API uses no cookies, so there is nothing for antiforgery tokens to protect (see primer).
            .DisableAntiforgery();

        catalog.MapGet("/imports/{id:guid}", GetAsync)
            .WithName("GetCatalogImport")
            .WithSummary("One import: game version, time and what it changed");

        return catalog;
    }

    private static async Task<Results<Created<ImportResponse>, ValidationProblem, ProblemHttpResult>> ImportAsync(
        IFormFile? file,
        [FromForm] string? gameVersion,
        ImportCatalogHandler handler,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> errors = [];
        if (file is null || file.Length == 0)
        {
            errors["file"] = ["Upload the game's Docs file (en-US.json)."];
        }

        if (string.IsNullOrWhiteSpace(gameVersion))
        {
            errors["gameVersion"] = ["Give the game version the Docs file belongs to, e.g. \"1.1\"."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        try
        {
            await using Stream docsFile = file!.OpenReadStream();
            CatalogImport import = await handler.HandleAsync(new ImportCatalogCommand(docsFile, gameVersion!), cancellationToken);

            return TypedResults.Created($"/api/v1/catalog/imports/{import.Id.Id}", ImportResponse.From(import));
        }
        catch (InvalidDocsFileException e)
        {
            return TypedResults.Problem(
                title: "The file is not a Docs file the catalog can import",
                detail: e.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity,
                extensions: new Dictionary<string, object?> { ["code"] = InvalidDocsFileCode });
        }
    }

    private static async Task<Results<Ok<ImportResponse>, NotFound>> GetAsync(
        Guid id, CatalogDbContext db, CancellationToken cancellationToken)
    {
        CatalogImport? import = await db.Imports.AsNoTracking()
            .SingleOrDefaultAsync(i => i.Id == CatalogImportId.Of(id), cancellationToken);

        return import is null ? TypedResults.NotFound() : TypedResults.Ok(ImportResponse.From(import));
    }
}