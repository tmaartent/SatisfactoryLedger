using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using SatisfactoryLedger.World.Persistence;
using SatisfactoryLedger.World.Simulation;

using Wolverine;

namespace SatisfactoryLedger.World.Analysis;

/// <summary>
/// At startup, asks for an analysis of every snapshot without a current one (ADR-0017, ADR-0018): still pending,
/// failed, computed with an older analysis version, or imported before analyses were stored.
/// </summary>
/// <remarks>
/// A <see cref="BackgroundService"/> starts while the host is still starting, before Wolverine can publish. So it
/// waits for <see cref="IHostApplicationLifetime.ApplicationStarted"/>. A failure is logged, not thrown: by default an
/// exception from a background service stops the whole host.
/// </remarks>
internal sealed partial class AnalysisRequeue(IServiceScopeFactory scopes, IHostApplicationLifetime lifetime, ILogger<AnalysisRequeue> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!await StartedAsync(stoppingToken))
        {
            return;
        }

        try
        {
            await using AsyncServiceScope scope = scopes.CreateAsyncScope();
            List<Guid> snapshots = await ToAnalyseAsync(scope.ServiceProvider.GetRequiredService<WorldDbContext>(), stoppingToken);
            IMessageBus bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            foreach (Guid id in snapshots)
            {
                await bus.PublishAsync(new SnapshotImported(id));
            }

            if (snapshots.Count > 0)
            {
                LogRequeued(logger, snapshots.Count);
            }
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            LogRequeueFailed(logger, exception);
        }
    }

    /// <summary>Waits until the host has started; false when it stops first.</summary>
    private async Task<bool> StartedAsync(CancellationToken stoppingToken)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration onStarted = lifetime.ApplicationStarted.Register(() => started.TrySetResult());
        using CancellationTokenRegistration onStopping = stoppingToken.Register(() => started.TrySetCanceled(stoppingToken));
        try
        {
            await started.Task;
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>The snapshots whose analysis isn't current, oldest first.</summary>
    public static async Task<List<Guid>> ToAnalyseAsync(WorldDbContext db, CancellationToken cancellationToken) =>
        await db.Snapshots.AsNoTracking()
            .Where(snapshot => !db.Analyses.Any(analysis =>
                analysis.SnapshotId == snapshot.Id && analysis.Status == AnalysisStatus.Ready && analysis.Version == AnalysisDocument.CurrentVersion))
            .OrderBy(snapshot => snapshot.ImportedAt)
            .Select(snapshot => snapshot.Id)
            .ToListAsync(cancellationToken);

    [LoggerMessage(Level = LogLevel.Information, Message = "Asked for the analysis of {Count} snapshots without a current one")]
    private static partial void LogRequeued(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Couldn't ask for the analysis of the snapshots without a current one; they'll be asked for at the next start")]
    private static partial void LogRequeueFailed(ILogger logger, Exception exception);
}