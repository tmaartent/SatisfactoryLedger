using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

/// <summary>
/// Analyses a snapshot when World says it was imported: fetch its flow network, simulate it, send the analysis back
/// (ADR-0018). Wolverine finds this handler by its name and calls <c>HandleAsync</c> for each message.
/// </summary>
public static partial class SnapshotImportedHandler
{
    public static async Task HandleAsync(SnapshotImported message, WorldClient world, ILogger<SnapshotImportedHandlerLog> logger, CancellationToken cancellationToken)
    {
        FlowNetworkDocument? network = await world.GetFlowNetworkAsync(message.SnapshotId, cancellationToken);
        if (network is null)
        {
            // Deleted since it was imported: nothing to analyse.
            return;
        }

        AnalysisDocument analysis;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            analysis = AnalysisDocument.Compute(network.ToNetwork());
        }
        // The simulation itself failed: tell World why. A failing call to World is left to Wolverine to retry.
        catch (Exception e) when (e is not (OperationCanceledException or OutOfMemoryException))
        {
            LogFailed(logger, e, message.SnapshotId);
            await world.PutFailureAsync(message.SnapshotId, e.Message, cancellationToken);
            return;
        }

        await world.PutAnalysisAsync(message.SnapshotId, new AnalysisResult(watch.Elapsed, analysis), cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The analysis of snapshot {SnapshotId} failed")]
    private static partial void LogFailed(ILogger logger, Exception exception, Guid snapshotId);
}

/// <summary>The log category of <see cref="SnapshotImportedHandler"/>: a static class can't be a type argument.</summary>
public sealed class SnapshotImportedHandlerLog;

/// <summary>World's internal endpoints for the analysis service (ADR-0018), as a typed HttpClient.</summary>
public sealed class WorldClient(HttpClient http)
{
    /// <returns>Null when World has no such snapshot.</returns>
    public async Task<FlowNetworkDocument?> GetFlowNetworkAsync(Guid snapshotId, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await http.GetAsync(new Uri($"/internal/world/snapshots/{snapshotId}/flow-network", UriKind.Relative), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return FlowNetworkDocument.FromJson(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public async Task PutAnalysisAsync(Guid snapshotId, AnalysisResult result, CancellationToken cancellationToken)
    {
        using var content = new StringContent(JsonSerializer.Serialize(result, SimulationJson.Options), Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await http.PutAsync(new Uri($"/internal/world/snapshots/{snapshotId}/analysis", UriKind.Relative), content, cancellationToken);
        EnsureStoredOrGone(response);
    }

    public async Task PutFailureAsync(Guid snapshotId, string error, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await http.PutAsJsonAsync(
            new Uri($"/internal/world/snapshots/{snapshotId}/analysis/failure", UriKind.Relative), new { error }, cancellationToken);
        EnsureStoredOrGone(response);
    }

    /// <summary>404 means the snapshot was deleted meanwhile: there's nothing to store it for.</summary>
    private static void EnsureStoredOrGone(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            response.EnsureSuccessStatusCode();
        }
    }
}
