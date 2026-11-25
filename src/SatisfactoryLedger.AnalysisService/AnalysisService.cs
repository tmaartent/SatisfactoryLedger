using Polly;

using SatisfactoryLedger.World.Contracts.Analysis;

using Wolverine;
using Wolverine.ErrorHandling;
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
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        // World takes half a minute to make an arrangement's problem on a big world: past the standard handler's
        // 10-second attempt, which the service defaults give every client. This client gets the same handler with room.
        // RemoveAllResilienceHandlers is marked for evaluation (EXTEXP0001); it's the documented way to replace the defaults.
#pragma warning disable EXTEXP0001
        world.RemoveAllResilienceHandlers().AddStandardResilienceHandler(options =>
#pragma warning restore EXTEXP0001
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(2);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
            // The circuit breaker samples at least twice an attempt's time.
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(4);
        });
        configureWorld?.Invoke(world);

        builder.UseWolverine(options =>
        {
            options.ServiceName = "analysis";
            // A handler is cancelled after 60 seconds by default. An arrangement takes its solver's time (up to 10
            // minutes) plus half a minute for World to make the problem: room for the longest.
            options.DefaultExecutionTimeout = TimeSpan.FromMinutes(15);
            // World couldn't be reached, after the World client's own retries (a few seconds): try the message again
            // later, for up to an hour, before it goes to the error queue (decided 2026-11-25, step 99). Without this it
            // went there at once, and the snapshot stayed pending until the API restarted. The handlers catch what
            // fails in their own work; what's left is a call to World. Scheduled tries are kept in memory: a restart of
            // this service loses them, and the API queues what's still pending when it starts.
            options.OnException(
                    exception => exception is HttpRequestException or ExecutionRejectedException,
                    "World can't be reached")
                .RetryWithCooldown(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2))
                .Then.ScheduleRetry(Enumerable.Repeat(TimeSpan.FromMinutes(5), 12).ToArray());
            options.UseRabbitMqUsingNamedConnection(AnalysisMessaging.RabbitMqConnectionName).AutoProvision();
            // Inline: a message is handled as it arrives, and RabbitMQ gets its ack only after the handler is done. A
            // crash during a simulation leaves it on the queue. (Buffered would take it into memory first.) Inline
            // also means one snapshot at a time: a big world's simulation takes all of a core for seconds.
            options.ListenToRabbitQueue(AnalysisMessaging.SnapshotsToAnalyseQueue).ProcessInline();
            // Arrangements on a queue of their own, one at a time: a run takes up to its solver's time limit on all cores.
            options.ListenToRabbitQueue(AnalysisMessaging.ArrangementsToComputeQueue).ProcessInline();
            options.Discovery.IncludeAssembly(typeof(AnalysisService).Assembly);
            // A typed HttpClient is made by a factory, so Wolverine's generated handler code can't build it inline:
            // allow looking it up in the container, for this type only.
            options.CodeGeneration.AlwaysUseServiceLocationFor<WorldClient>();
        });

        return builder;
    }
}