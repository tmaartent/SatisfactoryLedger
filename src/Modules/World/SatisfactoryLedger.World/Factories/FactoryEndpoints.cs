using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

using SatisfactoryLedger.World.Persistence;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Factories;

internal static class FactoryEndpoints
{
    public const string UnknownWorldCode = "World.UnknownWorld";
    public const string NotAnOutputBeltCode = "World.NotAnOutputBelt";
    public const string NotMachinesCode = "World.NotMachines";
    public const string FactoryNameTakenCode = "World.FactoryNameTaken";
    public const string MachinesTakenCode = "World.MachinesTaken";
    public const string UnknownInputItemsCode = "World.UnknownInputItems";

    public static RouteGroupBuilder MapFactoryEndpoints(this RouteGroupBuilder world)
    {
        world.MapGet("/snapshots/{id:guid}/output-belts", GetOutputBeltsAsync)
            .WithName("GetOutputBelts")
            .WithSummary("Candidate output belts in a snapshot: where items leave a group of machines (BR-W12)");

        world.MapPost("/factories", DefineAsync)
            .WithName("DefineFactory")
            .WithSummary("Define a factory in a world by its machines (BR-W2)")
            .WithDescription("A factory belongs to a world (a session name) and is traced in every snapshot of it. Its machines must be machines in the world's latest snapshot. Instead of machines, an output belt and input items can still be given: the machines are then traced from that belt.")
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        world.MapGet("/factories", ListAsync)
            .WithName("GetFactories")
            .WithSummary("The factories defined in a world: ?world=<session name>");

        world.MapGet("/factories/{id:guid}", GetAsync)
            .WithName("GetFactory")
            .WithSummary("One factory definition");

        world.MapGet("/snapshots/{id:guid}/factories", TraceAsync)
            .WithName("TraceFactories")
            .WithSummary("Every factory of the snapshot's world, traced in this snapshot (BR-W3, BR-W11)");

        return world;
    }

    private static async Task<Results<Ok<IReadOnlyList<OutputBeltSuggestion>>, NotFound>> GetOutputBeltsAsync(
        Guid id, WorldDbContext db, CancellationToken cancellationToken)
    {
        var snapshot = await db.LoadSnapshotAsync(id, cancellationToken);
        return snapshot is null ? TypedResults.NotFound() : TypedResults.Ok(OutputBeltSuggestions.For(snapshot.Value.Document));
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

        bool hasMachines = request.Machines is { Length: > 0 };
        bool hasOutputBelt = !string.IsNullOrWhiteSpace(request.OutputBelt);
        if (hasMachines == hasOutputBelt)
        {
            errors["machines"] = ["Give the ids of the factory's machines, or else the id of its output belt, but not both."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        DefineFactoryOutcome outcome = await handler.HandleAsync(
            new DefineFactoryCommand(request.World!, request.Name!, request.Machines ?? [], hasOutputBelt ? request.OutputBelt : null, request.InputItems ?? []),
            cancellationToken);

        return outcome switch
        {
            DefineFactoryOutcome.Defined defined =>
                TypedResults.Created($"/api/v1/world/factories/{defined.Factory.Id}", FactoryResponse.From(defined.Factory)),
            DefineFactoryOutcome.UnknownWorld => Problem(StatusCodes.Status422UnprocessableEntity, UnknownWorldCode,
                "There's no world with this name", $"No snapshot has the session name \"{request.World}\". Import a save of it first."),
            DefineFactoryOutcome.NotMachines notMachines => Problem(StatusCodes.Status422UnprocessableEntity, NotMachinesCode,
                "Some ids are not machines or generators in this world",
                $"Not machines or generators in the world's latest snapshot: {string.Join(", ", notMachines.Ids)}.",
                ("ids", notMachines.Ids)),
            DefineFactoryOutcome.NotAnOutputBelt => Problem(StatusCodes.Status422UnprocessableEntity, NotAnOutputBeltCode,
                "The output belt is not a belt in this world",
                $"{request.OutputBelt} is not a belt or lift fed by machines in the world's latest snapshot."),
            DefineFactoryOutcome.UnknownInputItems unknown => Problem(StatusCodes.Status422UnprocessableEntity, UnknownInputItemsCode,
                "The Catalog doesn't know some input items",
                $"Unknown items: {string.Join(", ", unknown.Items)}. Input items are class names, as listed at GET /api/v1/catalog/items.",
                ("items", unknown.Items)),
            DefineFactoryOutcome.NameTaken => Problem(StatusCodes.Status409Conflict, FactoryNameTakenCode,
                "The world already has a factory with this name", $"\"{request.Name}\" is taken in {request.World}."),
            DefineFactoryOutcome.MachinesTaken taken => Problem(StatusCodes.Status409Conflict, MachinesTakenCode,
                "Some machines already belong to another factory",
                $"A machine belongs to one factory at most: {string.Join(", ", taken.Machines.Select(machine => $"{machine.Id} is in \"{machine.Factory}\""))}.",
                ("taken", taken.Machines)),
            _ => throw new InvalidOperationException($"Unhandled outcome {outcome}"),
        };
    }

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