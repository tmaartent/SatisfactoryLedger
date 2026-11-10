using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

using SatisfactoryLedger.World.Analysis;
using SatisfactoryLedger.World.Persistence;
using SatisfactoryLedger.World.SaveFile;

using SatisfactoryLedger.World.Contracts.Analysis;
namespace SatisfactoryLedger.World.Snapshots;

internal static class SnapshotEndpoints
{
    public const string InvalidSaveFileCode = "World.InvalidSaveFile";

    public static RouteGroupBuilder MapSnapshotEndpoints(this RouteGroupBuilder world)
    {
        world.MapPost("/snapshots", ImportAsync)
            .WithName("ImportSave")
            .WithSummary("Import a save file (.sav) as a new world snapshot")
            .WithDescription("Every import is kept. Recipes the Catalog doesn't know are listed in unknownRecipes; buildables that aren't modelled yet in unsupported. Its analysis is computed in the background: analysis says pending until it's ready.")
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .DisableAntiforgery();

        world.MapGet("/snapshots", ListAsync)
            .WithName("GetSnapshots")
            .WithSummary("All imported snapshots, newest first");

        world.MapGet("/snapshots/{id:guid}", GetAsync)
            .WithName("GetSnapshot")
            .WithSummary("One snapshot: when it was saved and imported, what it contains, and what the import couldn't place");

        world.MapDelete("/snapshots/{id:guid}", DeleteAsync)
            .WithName("DeleteSnapshot")
            .WithSummary("Delete a snapshot and its analysis")
            .WithDescription("The world's factories stay: they belong to the world, and are traced in its next snapshot (BR-W2). An analysis still being computed for it is dropped.");

        return world;
    }

    // ExecuteDeleteAsync deletes in the database without loading the snapshot (its document is large). The database
    // deletes the analysis with it (a cascading foreign key).
    private static async Task<Results<NoContent, NotFound>> DeleteAsync(Guid id, WorldDbContext db, CancellationToken cancellationToken)
    {
        int deleted = await db.Snapshots.Where(snapshot => snapshot.Id == id).ExecuteDeleteAsync(cancellationToken);
        return deleted == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
    }

    private static async Task<Results<Created<SnapshotResponse>, ValidationProblem, ProblemHttpResult>> ImportAsync(
        IFormFile? file, ImportSaveHandler handler, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = ["Upload a save file (.sav) from %LOCALAPPDATA%\\FactoryGame\\Saved\\SaveGames."]
            });
        }

        try
        {
            await using Stream save = file.OpenReadStream();
            StoredSnapshot snapshot = await handler.HandleAsync(new ImportSaveCommand(save), cancellationToken);

            return TypedResults.Created($"/api/v1/world/snapshots/{snapshot.Id}", SnapshotResponse.From(snapshot, AnalysisStatus.Pending));
        }
        catch (SaveFileException e)
        {
            return TypedResults.Problem(
                title: "The file is not a save the world can import",
                detail: e.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity,
                extensions: new Dictionary<string, object?> { ["code"] = InvalidSaveFileCode });
        }
    }

    // The content column (megabytes for a big world) is never selected here: lists and details only need the columns.
    private static async Task<Ok<List<SnapshotSummaryResponse>>> ListAsync(WorldDbContext db, CancellationToken cancellationToken)
    {
        List<SnapshotSummaryResponse> snapshots = await db.Snapshots.AsNoTracking()
            .OrderByDescending(snapshot => snapshot.ImportedAt)
            .Select(snapshot => new SnapshotSummaryResponse(
                snapshot.Id, snapshot.SessionName, snapshot.GameBuild, snapshot.SavedAt, snapshot.ImportedAt, snapshot.Counts,
                db.Analyses.Where(analysis => analysis.SnapshotId == snapshot.Id && analysis.Version == AnalysisDocument.CurrentVersion)
                    .Select(analysis => (AnalysisStatus?)analysis.Status).FirstOrDefault() ?? AnalysisStatus.Pending))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(snapshots);
    }

    private static async Task<Results<Ok<SnapshotResponse>, NotFound>> GetAsync(
        Guid id, WorldDbContext db, CancellationToken cancellationToken)
    {
        var found = await db.Snapshots.AsNoTracking()
            .Where(snapshot => snapshot.Id == id)
            .Select(snapshot => new
            {
                snapshot.Id,
                snapshot.SessionName,
                snapshot.GameBuild,
                snapshot.SavedAt,
                snapshot.ImportedAt,
                snapshot.Counts,
                snapshot.UnknownRecipes,
                snapshot.Unsupported,
                Analysis = db.Analyses.Where(analysis => analysis.SnapshotId == snapshot.Id && analysis.Version == AnalysisDocument.CurrentVersion)
                    .Select(analysis => (AnalysisStatus?)analysis.Status).FirstOrDefault() ?? AnalysisStatus.Pending,
            })
            .SingleOrDefaultAsync(cancellationToken);

        return found is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(new SnapshotResponse(
                found.Id, found.SessionName, found.GameBuild, found.SavedAt, found.ImportedAt, found.Counts, found.UnknownRecipes,
                [.. found.Unsupported.Select(unsupported => new UnsupportedResponse(unsupported.ClassName, unsupported.Count))],
                found.Analysis));
    }
}