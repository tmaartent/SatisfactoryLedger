using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

using SatisfactoryLedger.World.Analysis;
using SatisfactoryLedger.World.Contracts.Analysis;
using SatisfactoryLedger.World.Persistence;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Factories;

internal static class FactoryEndpoints
{
    public const string UnknownWorldCode = "World.UnknownWorld";
    public const string NotMachinesCode = "World.NotMachines";
    public const string FactoryNameTakenCode = "World.FactoryNameTaken";
    public const string MachinesTakenCode = "World.MachinesTaken";

    public static RouteGroupBuilder MapFactoryEndpoints(this RouteGroupBuilder world)
    {
        world.MapPost("/factories", DefineAsync)
            .WithName("DefineFactory")
            .WithSummary("Define a factory in a world by its machines (BR-W2)")
            .WithDescription("A factory belongs to a world (a session name) and is traced in every snapshot of it. Its machines must be machines or generators in the world's latest snapshot, and none of them another factory's.")
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        world.MapGet("/factories", ListAsync)
            .WithName("GetFactories")
            .WithSummary("The factories defined in a world: ?world=<session name>");

        world.MapGet("/factories/{id:guid}", GetAsync)
            .WithName("GetFactory")
            .WithSummary("One factory definition");

        world.MapPut("/factories/{id:guid}", EditAsync)
            .WithName("EditFactory")
            .WithSummary("Give a factory a new name and machines (BR-W2, BR-W5, BR-W11)")
            .WithDescription("Its machines are all of them after the edit, checked as when defining it: machines or generators in its world's latest snapshot, and none of them another factory's.")
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        world.MapDelete("/factories/{id:guid}", DeleteAsync)
            .WithName("DeleteFactory")
            .WithSummary("Delete a factory definition: its machines belong to no factory again");

        world.MapGet("/snapshots/{id:guid}/factories", TraceAsync)
            .WithName("TraceFactories")
            .WithSummary("Every factory of the snapshot's world, traced in this snapshot (BR-W3, BR-W11)");

        // A POST that stores nothing: a selection can hold hundreds of ids, too many for a query string.
        world.MapPost("/snapshots/{id:guid}/factory-preview", PreviewAsync)
            .WithName("PreviewFactory")
            .WithSummary("What chosen machines would make a factory in this snapshot, before it's saved: the logistics it would own and its outputs and inputs (BR-W3, BR-W4)");

        return world;
    }

    private static async Task<Results<Ok<FactoryPreview>, NotFound>> PreviewAsync(
        Guid id, FactoryPreviewRequest request, WorldDbContext db, CancellationToken cancellationToken)
    {
        var snapshot = await db.LoadSnapshotAsync(id, cancellationToken);
        if (snapshot is null)
        {
            return TypedResults.NotFound();
        }

        // What crosses now, once the snapshot's analysis is stored; the preview doesn't ask for one.
        StoredAnalysis? stored = await db.Analyses.AsNoTracking().SingleOrDefaultAsync(analysis => analysis.SnapshotId == id, cancellationToken);
        AnalysisDocument? analysis = stored is { IsCurrent: true, Content: { } content } ? AnalysisDocument.FromJson(content) : null;
        return TypedResults.Ok(FactoryTraces.Preview(snapshot.Value.Document, request.Machines ?? [], analysis));
    }

    private static async Task<Results<Created<FactoryResponse>, ValidationProblem, ProblemHttpResult>> DefineAsync(
        DefineFactoryRequest request, DefineFactoryHandler handler, CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> errors = [];
        if (string.IsNullOrWhiteSpace(request.World))
        {
            errors["world"] = ["Give the world's session name, as shown on its snapshots."];
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["name"] = ["Give the factory a name."];
        }

        if (request.Machines is not { Length: > 0 })
        {
            errors["machines"] = ["Give the ids of the factory's machines."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        DefineFactoryOutcome outcome = await handler.HandleAsync(
            new DefineFactoryCommand(request.World!, request.Name!, request.Machines!),
            cancellationToken);

        return outcome is DefineFactoryOutcome.Defined defined
            ? TypedResults.Created($"/api/v1/world/factories/{defined.Factory.Id}", FactoryResponse.From(defined.Factory))
            : ProblemOf(outcome, request.Name!, request.World!);
    }

    private static async Task<Results<Ok<FactoryResponse>, NotFound, ValidationProblem, ProblemHttpResult>> EditAsync(
        Guid id, EditFactoryRequest request, DefineFactoryHandler handler, CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> errors = [];
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["name"] = ["Give the factory a name."];
        }

        if (request.Machines is not { Length: > 0 })
        {
            errors["machines"] = ["Give the ids of the factory's machines."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        DefineFactoryOutcome outcome = await handler.EditAsync(new EditFactoryCommand(id, request.Name!, request.Machines!), cancellationToken);
        return outcome switch
        {
            DefineFactoryOutcome.Defined edited => TypedResults.Ok(FactoryResponse.From(edited.Factory)),
            DefineFactoryOutcome.UnknownFactory => TypedResults.NotFound(),
            _ => ProblemOf(outcome, request.Name!, world: null),
        };
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(Guid id, WorldDbContext db, CancellationToken cancellationToken)
    {
        StoredFactory? factory = await db.Factories.FindAsync([id], cancellationToken);
        if (factory is null)
        {
            return TypedResults.NotFound();
        }

        db.Factories.Remove(factory);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>The problem for an outcome that isn't a factory defined or edited.</summary>
    private static ProblemHttpResult ProblemOf(DefineFactoryOutcome outcome, string name, string? world) => outcome switch
    {
        DefineFactoryOutcome.UnknownWorld => Problem(StatusCodes.Status422UnprocessableEntity, UnknownWorldCode,
            "There's no world with this name", $"No snapshot has the session name \"{world}\". Import a save of it first."),
        DefineFactoryOutcome.NotMachines notMachines => Problem(StatusCodes.Status422UnprocessableEntity, NotMachinesCode,
            "Some ids are not machines or generators in this world",
            $"Not machines or generators in the world's latest snapshot: {string.Join(", ", notMachines.Ids)}.",
            ("ids", notMachines.Ids)),
        DefineFactoryOutcome.NameTaken => Problem(StatusCodes.Status409Conflict, FactoryNameTakenCode,
            "The world already has a factory with this name", $"\"{name}\" is taken in this world."),
        DefineFactoryOutcome.MachinesTaken taken => Problem(StatusCodes.Status409Conflict, MachinesTakenCode,
            "Some machines already belong to another factory",
            $"A machine belongs to one factory at most: {string.Join(", ", taken.Machines.Select(machine => $"{machine.Id} is in \"{machine.Factory}\""))}.",
            ("taken", taken.Machines)),
        _ => throw new InvalidOperationException($"Unhandled outcome {outcome}"),
    };

    private static async Task<Ok<List<FactoryResponse>>> ListAsync(string? world, WorldDbContext db, CancellationToken cancellationToken)
    {
        List<StoredFactory> factories = await db.Factories.AsNoTracking()
            .Where(factory => factory.World == world)
            .OrderBy(factory => factory.Name)
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(factories.Select(FactoryResponse.From).ToList());
    }

    private static async Task<Results<Ok<FactoryResponse>, NotFound>> GetAsync(Guid id, WorldDbContext db, CancellationToken cancellationToken)
    {
        StoredFactory? factory = await db.Factories.AsNoTracking().SingleOrDefaultAsync(f => f.Id == id, cancellationToken);
        return factory is null ? TypedResults.NotFound() : TypedResults.Ok(FactoryResponse.From(factory));
    }

    private static async Task<Results<Ok<IReadOnlyList<FactoryTrace>>, NotFound>> TraceAsync(
        Guid id, WorldDbContext db, CancellationToken cancellationToken)
    {
        var snapshot = await db.LoadSnapshotAsync(id, cancellationToken);
        if (snapshot is null)
        {
            return TypedResults.NotFound();
        }

        List<StoredFactory> factories = await db.Factories.AsNoTracking()
            .Where(factory => factory.World == snapshot.Value.World)
            .OrderBy(factory => factory.Name)
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(FactoryTraces.TraceAll(snapshot.Value.Document, factories));
    }

    /// <param name="extra">More machine-readable fields next to <c>code</c>, so a client doesn't have to parse <c>detail</c>.</param>
    private static ProblemHttpResult Problem(int status, string code, string title, string detail, params (string Name, object? Value)[] extra)
    {
        Dictionary<string, object?> extensions = new() { ["code"] = code };
        foreach ((string name, object? value) in extra)
        {
            extensions[name] = value;
        }

        return TypedResults.Problem(title: title, detail: detail, statusCode: status, extensions: extensions);
    }
}