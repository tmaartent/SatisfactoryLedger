using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using SatisfactoryLedger.World.Persistence;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Factories;

/// <summary>
/// At startup, converts the factories still defined by an output belt and input items into their machines, traced in
/// their world's latest snapshot (BR-W2). One that can't be traced there stays as it was, is logged, and is tried
/// again at the next start.
/// </summary>
/// <remarks>A failure is logged, not thrown: by default an exception from a background service stops the whole host.</remarks>
internal sealed partial class FactoryConversion(IServiceScopeFactory scopes, ILogger<FactoryConversion> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using AsyncServiceScope scope = scopes.CreateAsyncScope();
            IReadOnlyList<string> notConverted = await ConvertAsync(scope.ServiceProvider.GetRequiredService<WorldDbContext>(), stoppingToken);
            if (notConverted.Count > 0)
            {
                LogNotConverted(logger, notConverted.Count, string.Join(", ", notConverted));
            }
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            LogConversionFailed(logger, exception);
        }
    }

    /// <summary>Converts what it can; returns the names (world: factory) of the factories it couldn't convert.</summary>
    public static async Task<IReadOnlyList<string>> ConvertAsync(WorldDbContext db, CancellationToken cancellationToken)
    {
        List<StoredFactory> unconverted = await db.Factories.Where(factory => factory.OutputBelt != null).ToListAsync(cancellationToken);
        List<string> notConverted = [];
        foreach (IGrouping<string, StoredFactory> world in unconverted.GroupBy(factory => factory.World, StringComparer.Ordinal))
        {
            SnapshotDocument? latest = await db.LoadLatestOfWorldAsync(world.Key, cancellationToken);
            notConverted.AddRange(world.Where(factory => latest is null || !factory.ConvertIn(latest)).Select(factory => $"{world.Key}: {factory.Name}"));
        }

        await db.SaveChangesAsync(cancellationToken);
        return notConverted;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Count} factories defined by an output belt can't be traced in their world's latest snapshot, so they weren't converted to their machines: {Factories}")]
    private static partial void LogNotConverted(ILogger logger, int count, string factories);

    [LoggerMessage(Level = LogLevel.Error, Message = "Couldn't convert the factories defined by an output belt; they'll be tried again at the next start")]
    private static partial void LogConversionFailed(ILogger logger, Exception exception);
}
