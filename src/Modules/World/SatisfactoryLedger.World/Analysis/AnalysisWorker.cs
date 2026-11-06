using System.Diagnostics;
using System.Threading.Channels;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using SatisfactoryLedger.World.Persistence;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Analysis;

/// <summary>The snapshots waiting for their analysis (ADR-0017): an in-process queue. The database says what's pending.</summary>
internal sealed class AnalysisQueue
{
    private readonly Channel<Guid> _snapshots = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(Guid snapshotId) => _snapshots.Writer.TryWrite(snapshotId);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken) => _snapshots.Reader.ReadAllAsync(cancellationToken);
}

/// <summary>Computes and stores one snapshot's analysis (ADR-0017). Does nothing when it's already current.</summary>
internal sealed partial class AnalyseSnapshotHandler(WorldDbContext db, TimeProvider clock, ILogger<AnalyseSnapshotHandler> logger)
{
    public async Task HandleAsync(Guid snapshotId, CancellationToken cancellationToken)
    {
        StoredAnalysis? analysis = await db.Analyses.FindAsync([snapshotId], cancellationToken);
        var snapshot = await db.LoadSnapshotAsync(snapshotId, cancellationToken);
        if (snapshot is null || analysis?.IsCurrent == true)
        {
            return;
        }

        if (analysis is null)
        {
            analysis = StoredAnalysis.PendingFor(snapshotId);
            db.Analyses.Add(analysis);
        }

        var watch = Stopwatch.StartNew();
        try
        {
            analysis.Content = AnalysisDocument.Compute(snapshot.Value.Document).ToJson();
            analysis.Status = AnalysisStatus.Ready;
            analysis.Version = AnalysisDocument.CurrentVersion;
            analysis.Error = null;
        }
        // An analysis that fails is stored as failed with its reason, so the API can say so; the worker carries on.
        catch (Exception e) when (e is not (OperationCanceledException or OutOfMemoryException))
        {
            analysis.Status = AnalysisStatus.Failed;
            analysis.Error = e.Message;
            LogFailed(logger, e, snapshotId);
        }

        analysis.ComputedAt = clock.GetUtcNow();
        analysis.Duration = watch.Elapsed;
        await db.SaveChangesAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The analysis of snapshot {SnapshotId} failed")]
    private static partial void LogFailed(ILogger logger, Exception exception, Guid snapshotId);
}

/// <summary>
/// The background worker (ADR-0017): at startup it queues every snapshot whose analysis isn't current, then analyses
/// the queued snapshots one at a time. Like a Spring <c>@Async</c> task executor with one thread.
/// </summary>
internal sealed partial class AnalysisWorker(IServiceScopeFactory scopes, AnalysisQueue queue, ILogger<AnalysisWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using (AsyncServiceScope scope = scopes.CreateAsyncScope())
        {
            foreach (Guid id in await ToAnalyseAsync(scope.ServiceProvider.GetRequiredService<WorldDbContext>(), stoppingToken))
            {
                queue.Enqueue(id);
            }
        }

        await foreach (Guid id in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using AsyncServiceScope scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<AnalyseSnapshotHandler>().HandleAsync(id, stoppingToken);
            }
            // One snapshot going wrong (it was deleted while queued, the database is briefly away) mustn't stop the worker.
            catch (Exception e) when (e is not (OperationCanceledException or OutOfMemoryException))
            {
                LogFailed(logger, e, id);
            }
        }
    }

    /// <summary>
    /// The snapshots whose analysis isn't current: pending, failed (maybe for a reason that's gone), computed with an
    /// older analysis version, or imported before analyses were stored.
    /// </summary>
    public static async Task<List<Guid>> ToAnalyseAsync(WorldDbContext db, CancellationToken cancellationToken) =>
        await db.Snapshots.AsNoTracking()
            .Where(snapshot => !db.Analyses.Any(analysis =>
                analysis.SnapshotId == snapshot.Id && analysis.Status == AnalysisStatus.Ready && analysis.Version == AnalysisDocument.CurrentVersion))
            .OrderBy(snapshot => snapshot.ImportedAt)
            .Select(snapshot => snapshot.Id)
            .ToListAsync(cancellationToken);

    [LoggerMessage(Level = LogLevel.Error, Message = "The background worker couldn't analyse snapshot {SnapshotId}")]
    private static partial void LogFailed(ILogger logger, Exception exception, Guid snapshotId);
}
