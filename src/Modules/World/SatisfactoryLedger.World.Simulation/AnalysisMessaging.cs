namespace SatisfactoryLedger.World.Simulation;

/// <summary>What the API and the analysis service agree on to find each other (ADR-0018).</summary>
public static class AnalysisMessaging
{
    /// <summary>The RabbitMQ queue World publishes <see cref="SnapshotImported"/> to, and the analysis service listens on.</summary>
    public const string SnapshotsToAnalyseQueue = "world.snapshots-to-analyse";

    /// <summary>The connection string name of RabbitMQ, as the AppHost gives it.</summary>
    public const string RabbitMqConnectionName = "rabbitmq";

    /// <summary>The API's name in the AppHost: the analysis service finds it by service discovery.</summary>
    public const string ApiServiceName = "api";
}
