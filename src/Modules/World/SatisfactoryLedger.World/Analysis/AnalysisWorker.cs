using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using SatisfactoryLedger.World.Persistence;
using SatisfactoryLedger.World.Simulation;

using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;

namespace SatisfactoryLedger.World.Analysis;

/// <summary>World's side of the messaging with the analysis service (ADR-0018).</summary>
internal static class AnalysisMessages
{
    /// <summary>The schema of Wolverine's outbox and inbox tables, next to the modules' own.</summary>
    public const string OutboxSchema = "wolverine";

    /// <summary>
    /// The import publishes <see cref="SnapshotImported"/> through the transactional outbox on PostgreSQL: the snapshot
    /// and its message are stored in one transaction, and Wolverine sends the message to RabbitMQ afterwards.
    /// </summary>
    public static WolverineOptions AddWorldMessaging(WolverineOptions options)
    {
        // By name: the connection string is read when the host starts, not here. The build-time OpenAPI generation runs
        // this without one.
        options.ServiceName = "ledger-api";
        options.PersistMessagesWithPostgresqlUsingNamedConnection(WorldDbContext.ConnectionStringName, OutboxSchema);
        options.UseEntityFrameworkCoreTransactions();
        options.UseRabbitMqUsingNamedConnection(AnalysisMessaging.RabbitMqConnectionName).AutoProvision();
        options.PublishMessage<SnapshotImported>().ToRabbitQueue(AnalysisMessaging.SnapshotsToAnalyseQueue).UseDurableOutbox();
        return options;
    }
}

/// <summary>
/// At startup, asks for an analysis of every snapshot without a current one (ADR-0017, ADR-0018): still pending,
/// failed, computed with an older analysis version, or imported before analyses were stored.
/// </summary>
internal sealed partial class AnalysisRequeue(IServiceScopeFactory scopes, ILogger<AnalysisRequeue> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
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
}
