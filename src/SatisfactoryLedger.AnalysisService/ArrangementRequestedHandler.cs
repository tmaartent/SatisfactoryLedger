using SatisfactoryLedger.World.Contracts.Arrangement;
using SatisfactoryLedger.World.Simulation;

namespace SatisfactoryLedger.AnalysisService;

/// <summary>
/// Computes an optimal arrangement when World asks (decided 2026-11-12): fetch its problem, solve it with CP-SAT, send
/// the proposal back. The ADR-0018 pattern, on a queue of its own.
/// </summary>
public static partial class ArrangementRequestedHandler
{
    public static async Task HandleAsync(
        ArrangementRequested message, WorldClient world, ILogger<ArrangementRequestedHandlerLog> logger, CancellationToken cancellationToken)
    {
        ArrangementProblemDocument? problem = await world.GetArrangementProblemAsync(message.ArrangementId, cancellationToken);
        if (problem is null)
        {
            // Its snapshot was deleted since: nothing to arrange.
            return;
        }

        ArrangementProposalDocument proposal;
        try
        {
            proposal = ArrangementDocuments.Solve(problem);
        }
        // The solver itself failed: tell World why. A failing call to World is left to Wolverine to retry.
        catch (Exception e) when (e is not (OperationCanceledException or OutOfMemoryException))
        {
            LogFailed(logger, e, message.ArrangementId);
            await world.PutArrangementFailureAsync(message.ArrangementId, e.Message, cancellationToken);
            return;
        }

        await world.PutArrangementAsync(message.ArrangementId, proposal, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The arrangement {ArrangementId} failed")]
    private static partial void LogFailed(ILogger logger, Exception exception, Guid arrangementId);
}
