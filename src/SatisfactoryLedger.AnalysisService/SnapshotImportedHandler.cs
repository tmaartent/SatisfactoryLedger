using SatisfactoryLedger.World.Simulation;

namespace SatisfactoryLedger.AnalysisService;

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