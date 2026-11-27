using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

using SatisfactoryLedger.World.Contracts.Arrangement;
using SatisfactoryLedger.World.Domain.Arrangement;
using SatisfactoryLedger.World.Persistence;
using SatisfactoryLedger.World.Snapshots;

using Wolverine.EntityFrameworkCore;

namespace SatisfactoryLedger.World.Arrangement;

/// <summary>The optimal arrangement (decided 2026-11-12): request a run, list a world's runs, read one, delete one.</summary>
internal static class ArrangementEndpoints
{
    public const string AnalysisNotReadyCode = "World.AnalysisNotReady";

    public static RouteGroupBuilder MapArrangementEndpoints(this RouteGroupBuilder world)
    {
        world.MapPost("/snapshots/{id:guid}/arrangements", RequestAsync)
            .WithName("RequestArrangement")
            .WithSummary("Ask where the factories in an area would best stand, on plots in rows and columns, so the belt work between them and their inputs is least")
            .WithDescription("Computed by the analysis service in the background from the snapshot, its factories and its analysis: 202 with the run, pending until it's ready. Every run is kept until it's deleted.")
            .ProducesProblem(StatusCodes.Status409Conflict);

        world.MapGet("/arrangements", ListAsync)
            .WithName("GetArrangements")
            .WithSummary("The runs of the optimal arrangement for a world, newest first: ?world=<session name>");

        world.MapGet("/arrangements/{id:guid}", GetAsync)
            .WithName("GetArrangement")
            .WithSummary("One run: its factories now and where it would place them, the fixed places, and the links");

        world.MapGet("/arrangements/{id:guid}/machines", MachinesAsync)
            .WithName("GetArrangementMachines")
            .WithSummary("The machines of a run's arranged factories as they stand now, with their footprints: to draw them moved with their factories")
            .WithDescription("Empty until the run's problem is made. Read from the run's snapshot, so asked once per run rather than with every look at it.");

        world.MapDelete("/arrangements/{id:guid}", DeleteAsync)
            .WithName("DeleteArrangement")
            .WithSummary("Delete a run, ready or still being computed: a proposal that comes back for it later is dropped");

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

        // Half a belt between factories: factories close to each other first, then to what feeds them (decided 2026-11-18).
        double spotWeight = request.SpotWeight ?? 0.5;

        // 8 foundations of 4 metres high: a stack of 16 belts on stackable poles 2 metres apart.
        double ceiling = request.CeilingMetres ?? 32;
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

        if (spotWeight < 0)
        {
            errors["spotWeight"] = ["Belts to nodes and stations can't be weighed below 0."];
        }

        if (ceiling < 2)
        {
            errors["ceiling"] = ["The belt floor needs a ceiling of at least 2 metres, for one belt."];
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
        if (await db.LoadSnapshotAsync(id, cancellationToken) is not (string world, SnapshotDocument snapshot))
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
            SpotWeight = spotWeight,
            CeilingMetres = ceiling,
            SupplyLines = request.SupplyLines ?? false,
            Footprints = request.Footprints ?? false,
            Exclusions = exclusions,
            Seconds = seconds,
            Status = ArrangementRunStatus.Pending,
            RequestedAt = clock.GetUtcNow(),
        };

        // Refused at once when the factories can't fit: fewer free plots than factories (2026-11-18: roads of 42
        // foundations by mistake left 18 plots for 48 factories, and the run came back with none found, a minute later).
        ArrangementProblem problem = await ArrangementProblems.PlacingAsync(db, run, snapshot, cancellationToken);
        if (!problem.HasRoom)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["area"] = [ArrangementProblems.NoRoom(problem, run.Footprints)] });
        }

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

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(Guid id, WorldDbContext db, CancellationToken cancellationToken)
    {
        StoredArrangement? run = await db.Arrangements.FindAsync([id], cancellationToken);
        if (run is null)
        {
            return TypedResults.NotFound();
        }

        db.Arrangements.Remove(run);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<ArrangementMachinesResponse>, NotFound>> MachinesAsync(Guid id, WorldDbContext db, CancellationToken cancellationToken)
    {
        StoredArrangement? run = await db.Arrangements.AsNoTracking().SingleOrDefaultAsync(arrangement => arrangement.Id == id, cancellationToken);
        if (run is null)
        {
            return TypedResults.NotFound();
        }

        var snapshot = run.Problem is null ? null : await db.LoadSnapshotAsync(run.SnapshotId, cancellationToken);
        if (run.Problem is not { } problemJson || snapshot is not { Document: var document })
        {
            return TypedResults.Ok(new ArrangementMachinesResponse([], []));
        }

        // Every factory of the world is matched, so a rebuilt machine goes to the factory it goes to everywhere (BR-W11).
        HashSet<Guid> ids = [.. ArrangementProblemDocument.FromJson(problemJson).Factories.Select(factory => Guid.Parse(factory.Id))];
        List<Factories.StoredFactory> factories = await db.Factories.AsNoTracking().Where(factory => factory.World == run.World).ToListAsync(cancellationToken);
        Dictionary<string, BuildableDocument> buildables = document.Buildables.ToDictionary(buildable => buildable.Id, StringComparer.Ordinal);
        List<ArrangedMachineResponse> machines = [.. Factories.SnapshotFactories.Of(document, factories).Factories
            .Where(traced => ids.Contains(traced.Factory.Id))
            .SelectMany(traced => traced.Machines
                .Select(machine => buildables.GetValueOrDefault(machine.Id))
                .OfType<BuildableDocument>()
                .Where(machine => machine.Position is not null)
                .Select(machine => new ArrangedMachineResponse(traced.Factory.Id, machine.Id, machine.ClassName, machine.Position!, machine.Yaw ?? 0)))];
        HashSet<string> classes = [.. machines.Select(machine => machine.ClassName)];
        return TypedResults.Ok(new ArrangementMachinesResponse(machines, [.. (document.Footprints ?? []).Where(footprint => classes.Contains(footprint.Id))]));
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
            return TypedResults.Ok(new ArrangementResponse(ArrangementSummary.From(run), [], [], [], [], [], []));
        }

        ArrangementProblemDocument problem = ArrangementProblemDocument.FromJson(problemJson);
        ArrangementProposalDocument? proposal = run.Proposal is { } proposalJson ? ArrangementProposalDocument.FromJson(proposalJson) : null;
        IReadOnlyDictionary<string, GridRectangleDocument> places = proposal?.Places ?? new Dictionary<string, GridRectangleDocument>();
        BeltFloor floor = run.Settings.Floor;
        List<Guid> ids = [.. problem.Factories.Select(factory => Guid.Parse(factory.Id))];
        Dictionary<Guid, string> names = await db.Factories.AsNoTracking()
            .Where(factory => ids.Contains(factory.Id))
            .ToDictionaryAsync(factory => factory.Id, factory => factory.Name, cancellationToken);

        // What a belt carries: as the analysis service says, or in runs from before it did, its link's load at its number
        // among the link's belts, or among its pipes.
        Dictionary<(string From, string To), ArrangementLinkDocument> links = [];
        foreach (ArrangementLinkDocument link in problem.Links)
        {
            links.TryAdd((link.From, link.To), link);
        }

        BeltLoadDocument? LoadOf(RoutedBeltDocument belt) =>
            links.GetValueOrDefault((belt.From, belt.To))?.Loads?.Where(load => load.Pipe == belt.Pipe).ElementAtOrDefault(belt.Index);

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
            problem.Links,
            [.. (proposal?.Belts ?? []).Select(belt => (Belt: belt, Load: belt.Load ?? LoadOf(belt))).Select(carried => new RoutedBeltResponse(
                carried.Belt.From,
                carried.Belt.To,
                [.. carried.Belt.Corners.Select(corner => grid.WorldOf(corner, floor))],
                carried.Belt.LeavesFloor,
                carried.Belt.Pipe,
                [.. (carried.Belt.Levels ?? []).Select(BeltFloor.HeightOf)],
                [.. (carried.Belt.Lifts ?? []).Select(lift => grid.WorldOf(lift, floor))],
                carried.Belt.Levels ?? [],
                carried.Load?.Item,
                carried.Load?.PerMinute,
                carried.Load?.Injection ?? false,
                carried.Load?.Overflow ?? false,
                carried.Belt.StartsOnBelt,
                carried.Belt.EndsOnBelt,
                [.. (carried.Belt.LastResortCrossings ?? []).Select(square => grid.WorldOf(square, floor))]))],
            proposal?.Unrouted ?? [],
            [.. (problem.Spots ?? [])
                .Where(spot => spot.Way is { Count: > 0 })
                .Select(spot => new ExistingWayResponse(spot.Id, [.. spot.Way!.Select(point => new WorldPoint(point.X, point.Y, point.Z))]))]));
    }
}
