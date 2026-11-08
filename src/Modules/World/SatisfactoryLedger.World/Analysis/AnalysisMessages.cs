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