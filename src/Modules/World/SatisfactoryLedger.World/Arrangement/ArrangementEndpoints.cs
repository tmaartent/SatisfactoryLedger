using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

using SatisfactoryLedger.World.Contracts.Arrangement;
using SatisfactoryLedger.World.Persistence;

using Wolverine.EntityFrameworkCore;

namespace SatisfactoryLedger.World.Arrangement;

/// <summary>The optimal arrangement (decided 2026-11-12): request a run, list a world's runs, read one.</summary>
internal static class ArrangementEndpoints
{
    public const string AnalysisNotReadyCode = "World.AnalysisNotReady";

    public static RouteGroupBuilder MapArrangementEndpoints(this RouteGroupBuilder world)
    {
        world.MapPost("/snapshots/{id:guid}/arrangements", RequestAsync)
            .WithName("RequestArrangement")
            .WithSummary("Ask where the factories in an area would best stand, on plots in rows and columns, so the belt work between them and their inputs is least")
            .WithDescription("Computed by the analysis service in the background from the snapshot, its factories and its analysis: 202 with the run, pending until it's ready. Every run is kept.")
            .ProducesProblem(StatusCodes.Status409Conflict);

        world.MapGet("/arrangements", ListAsync)
            .WithName("GetArrangements")
            .WithSummary("The runs of the optimal arrangement for a world, newest first: ?world=<session name>");

        world.MapGet("/arrangements/{id:guid}", GetAsync)
            .WithName("GetArrangement")
            .WithSummary("One run: its factories now and where it would place them, the fixed places, and the links");

        return world;
    }

    private static async Task<Results<Accepted<ArrangementSummary>, NotFound, ValidationProblem, ProblemHttpResult>> RequestAsync(
        // [FromServices]: without it, the build-time OpenAPI generation (where Wolverine isn't there) takes it for a body.
        Guid id, ArrangementRequest request, [FromServices] IDbContextOutbox<WorldDbContext> outbox, TimeProvider clock, CancellationToken cancellationToken)
    {
        // 6 by 6 foundations of 8 metres, with roads of 4 foundations: how the player builds.
        double plotWidth = request.PlotWidthMetres ?? 48;
        double plotDepth = request.PlotDepthMetres ?? 48;
        double roadX = request.RoadXMetres ?? 32;
        double roadY = request.RoadYMetres ?? 32;
        int together = request.Together ?? 2;
        List<WorldRectangle> exclusions = [.. request.Exclusions ?? []];
        int seconds = request.Seconds ?? 60;
        Dictionary<string, string[]> errors = [];
        if (plotWidth <= 0 || plotDepth <= 0)
        {
            errors["plot"] = ["A plot must be larger than 0 metres both ways."];
        }

        if (roadX < 0 || roadY < 0)
        {
            errors["road"] = ["A road can't be narrower than 0 metres."];
        }

        if (together < 0)
        {
            errors["together"] = ["Keeping together can't be weighed below 0."];
        }

        if (request.Area is { } drawn && (drawn.X1 <= drawn.X0 || drawn.Y1 <= drawn.Y0))
        {
            errors["area"] = ["Draw an area with some size."];
        }

        if (exclusions.Any(exclusion => exclusion.X1 <= exclusion.X0 || exclusion.Y1 <= exclusion.Y0))
        {
            errors["exclusions"] = ["Draw every exclusion with some size."];
        }

        if (seconds is < 1 or > 600)
        {
            errors["seconds"] = ["Give the solver between 1 and 600 seconds."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        WorldDbContext db = outbox.DbContext;
        string? world = await db.Snapshots.AsNoTracking().Where(snapshot => snapshot.Id == id).Select(snapshot => snapshot.SessionName).SingleOrDefaultAsync(cancellationToken);
        if (world is null)
        {
            return TypedResults.NotFound();
        }

        bool analysed = await db.Analyses.AsNoTracking().AnyAsync(analysis => analysis.SnapshotId == id && analysis.Status == Analysis.AnalysisStatus.Ready, cancellationToken);
        if (!analysed)
        {
            return TypedResults.Problem(
                title: "The snapshot isn't analysed yet",
                detail: "The arrangement weighs links by the flows of the snapshot's analysis. Try again once it's ready.",
                statusCode: StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?> { ["code"] = AnalysisNotReadyCode });
        }

        var run = new StoredArrangement
        {
            Id = Guid.CreateVersion7(),
            World = world,
            SnapshotId = id,
            AreaX0 = request.Area?.X0,
            AreaY0 = request.Area?.Y0,
            AreaX1 = request.Area?.X1,
            AreaY1 = request.Area?.Y1,
            PlotWidthMetres = plotWidth,
            PlotDepthMetres = plotDepth,
            RoadXMetres = roadX,
            RoadYMetres = roadY,
            Together = together,
            Exclusions = exclusions,
            Seconds = seconds,
            Status = ArrangementRunStatus.Pending,
            RequestedAt = clock.GetUtcNow(),
        };

        // The run and the message to the analysis service in one transaction (the transactional outbox, ADR-0018).
        db.Arrangements.Add(run);
        await outbox.PublishAsync(new ArrangementRequested(run.Id));
        await outbox.SaveChangesAndFlushMessagesAsync(cancellationToken);
        return TypedResults.Accepted($"/api/v1/world/arrangements/{run.Id}", ArrangementSummary.From(run));
    }

    private static async Task<Ok<List<ArrangementSummary>>> ListAsync(string? world, WorldDbContext db, CancellationToken cancellationToken)
    {
        List<StoredArrangement> runs = await db.Arrangements.AsNoTracking()
            .Where(run => run.World == world)
            .OrderByDescending(run => run.Id)
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(runs.Select(ArrangementSummary.From).ToList());
    }

    private static async Task<Results<Ok<ArrangementResponse>, NotFound>> GetAsync(Guid id, WorldDbContext db, CancellationToken cancellationToken)
    {
        StoredArrangement? run = await db.Arrangements.AsNoTracking().SingleOrDefaultAsync(arrangement => arrangement.Id == id, cancellationToken);
        if (run is null)
        {
            return TypedResults.NotFound();
        }

        if (run.Problem is not { } problemJson || run.Grid is not { } grid)
        {
            return TypedResults.Ok(new ArrangementResponse(ArrangementSummary.From(run), [], [], []));
        }

        ArrangementProblemDocument problem = ArrangementProblemDocument.FromJson(problemJson);
        IReadOnlyDictionary<string, GridRectangleDocument> places = run.Proposal is { } proposal
            ? ArrangementProposalDocument.FromJson(proposal).Places
            : new Dictionary<string, GridRectangleDocument>();
        List<Guid> ids = [.. problem.Factories.Select(factory => Guid.Parse(factory.Id))];
        Dictionary<Guid, string> names = await db.Factories.AsNoTracking()
            .Where(factory => ids.Contains(factory.Id))
            .ToDictionaryAsync(factory => factory.Id, factory => factory.Name, cancellationToken);

        return TypedResults.Ok(new ArrangementResponse(
            ArrangementSummary.From(run),
            [
                .. problem.Factories.Select(factory => new ArrangedFactoryResponse(
                    Guid.Parse(factory.Id),
                    names.GetValueOrDefault(Guid.Parse(factory.Id)),
                    grid.WorldOf(factory.Now),
                    places.TryGetValue(factory.Id, out GridRectangleDocument? place) ? grid.WorldOf(place) : null)),
            ],
            [.. problem.Fixed.Select(place => new FixedPlaceResponse(place.Id, grid.WorldOf(place.Space), place.Blocks))],
            problem.Links));
    }
}
