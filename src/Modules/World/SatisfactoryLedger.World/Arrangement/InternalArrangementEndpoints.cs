using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

using SatisfactoryLedger.World.Contracts.Analysis;
using SatisfactoryLedger.World.Contracts.Arrangement;
using SatisfactoryLedger.World.Domain.Arrangement;
using SatisfactoryLedger.World.Persistence;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Arrangement;

/// <summary>
/// What the analysis service calls for an arrangement: it fetches the problem and sends the proposal back. Not part of
/// the public API (the ADR-0018 pattern).
/// </summary>
internal static class InternalArrangementEndpoints
{
    public static RouteGroupBuilder MapInternalArrangementEndpoints(this RouteGroupBuilder world)
    {
        world.MapGet("/arrangements/{id:guid}/problem", GetProblemAsync);
        world.MapPut("/arrangements/{id:guid}/proposal", StoreProposalAsync);
        world.MapPut("/arrangements/{id:guid}/failure", StoreFailureAsync);
        return world;
    }

    /// <summary>Makes the run's problem from its snapshot, its world's factories and the snapshot's analysis, and keeps it.</summary>
    private static async Task<Results<ContentHttpResult, NotFound, Conflict>> GetProblemAsync(Guid id, WorldDbContext db, CancellationToken cancellationToken)
    {
        StoredArrangement? run = await db.Arrangements.SingleOrDefaultAsync(arrangement => arrangement.Id == id, cancellationToken);
        var snapshot = run is null ? null : await db.LoadSnapshotAsync(run.SnapshotId, cancellationToken);
        if (run is null || snapshot is null)
        {
            return TypedResults.NotFound();
        }

        Analysis.StoredAnalysis? stored = await db.Analyses.AsNoTracking().SingleOrDefaultAsync(analysis => analysis.SnapshotId == run.SnapshotId, cancellationToken);
        if (stored is not { Status: Analysis.AnalysisStatus.Ready, Content: { } content })
        {
            return TypedResults.Conflict();
        }

        (ArrangementProblem problem, ArrangementGrid grid, IReadOnlyDictionary<string, GridRectangle> spots, IReadOnlyDictionary<(string From, string To), IReadOnlyList<BeltLoadDocument>> loads) =
            await ArrangementProblems.MakeAsync(db, run, snapshot.Value.Document, content, cancellationToken);

        string json = WorldArrangement.ToDocument(problem, run.Seconds, run.Settings.Floor, spots, loads).ToJson();
        run.Problem = json;
        run.GridOriginX = grid.OriginX;
        run.GridOriginY = grid.OriginY;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Text(json, "application/json");
    }

    private static async Task<Results<NoContent, NotFound>> StoreProposalAsync(
        Guid id, ArrangementProposalDocument proposal, WorldDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        StoredArrangement? run = await db.Arrangements.SingleOrDefaultAsync(arrangement => arrangement.Id == id, cancellationToken);
        if (run is null)
        {
            return TypedResults.NotFound();
        }

        run.Proposal = proposal.ToJson();
        run.Duration = proposal.Duration;
        run.ComputedAt = clock.GetUtcNow();
        run.Status = ArrangementRunStatus.Ready;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, NotFound>> StoreFailureAsync(
        Guid id, ArrangementFailureRequest failure, WorldDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        StoredArrangement? run = await db.Arrangements.SingleOrDefaultAsync(arrangement => arrangement.Id == id, cancellationToken);
        if (run is null)
        {
            return TypedResults.NotFound();
        }

        run.Error = failure.Error ?? "The analysis service gave no reason";
        run.ComputedAt = clock.GetUtcNow();
        run.Status = ArrangementRunStatus.Failed;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
