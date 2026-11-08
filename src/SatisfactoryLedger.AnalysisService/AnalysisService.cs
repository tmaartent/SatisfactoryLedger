using SatisfactoryLedger.World.Simulation;

using Wolverine;
using Wolverine.RabbitMQ;

namespace SatisfactoryLedger.AnalysisService;

/// <summary>Sets up the analysis service (ADR-0018). Program.cs and the integration tests both use it.</summary>
public static class AnalysisService
{
    /// <param name="configureWorld">Lets a test point the World client at an in-memory API instead of service discovery.</param>
    public static IHostApplicationBuilder AddAnalysisService(this IHostApplicationBuilder builder, Action<IHttpClientBuilder>? configureWorld = null)
    {
        builder.AddServiceDefaults();

        IHttpClientBuilder world = builder.Services.AddHttpClient<WorldClient>(client =>
        {
            // "https+http://": service discovery tries HTTPS first, then HTTP.
            client.BaseAddress = new Uri($"https+http://{AnalysisMessaging.ApiServiceName}");
            client.Timeout = TimeSpan.FromMinutes(2);
        });
        configureWorld?.Invoke(world);

        builder.UseWolverine(options =>
        {
            options.ServiceName = "analysis";
            options.UseRabbitMqUsingNamedConnection(AnalysisMessaging.RabbitMqConnectionName).AutoProvision();
            // Inline: a message is handled as it arrives, and RabbitMQ gets its ack only after the handler is done. A
            // crash during a simulation leaves it on the queue. (Buffered would take it into memory first.) Inline
            // also means one snapshot at a time: a big world's simulation takes all of a core for seconds.
            options.ListenToRabbitQueue(AnalysisMessaging.SnapshotsToAnalyseQueue).ProcessInline();
            options.Discovery.IncludeAssembly(typeof(AnalysisService).Assembly);
            // A typed HttpClient is made by a factory, so Wolverine's generated handler code can't build it inline:
            // allow looking it up in the container, for this type only.
            options.CodeGeneration.AlwaysUseServiceLocationFor<WorldClient>();
        });

        return builder;
    }
}