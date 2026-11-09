using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using SatisfactoryLedger.World.Analysis;
using SatisfactoryLedger.World.Factories;
using SatisfactoryLedger.World.Map;
using SatisfactoryLedger.World.Persistence;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World;

/// <summary>The World module's only public type: the host uses it to register and map the module (ADR-0003, ADR-0014).</summary>
public static class WorldModule
{
    public static IHostApplicationBuilder AddWorldModule(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContext<WorldDbContext>(options =>
            WorldDbContext.Configure(options, builder.Configuration.GetConnectionString(WorldDbContext.ConnectionStringName)));

        // Adds Aspire's extras to that DbContext: a health check, tracing and metrics. Not its retries: EF's retrying
        // execution strategy refuses transactions it didn't start, and Wolverine's outbox opens one as soon as the import
        // publishes its message (ADR-0018). Wolverine retries the messaging itself.
        builder.EnrichNpgsqlDbContext<WorldDbContext>(settings => settings.DisableRetry = true);

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddScoped<ImportSaveHandler>();
        builder.Services.AddScoped<DefineFactoryHandler>();

        // ADR-0018: the analysis service computes each snapshot's analysis; World stores it (ADR-0017).
        builder.Services.AddScoped<StoreAnalysisHandler>();
        builder.Services.AddHostedService<AnalysisRequeue>();
        builder.Services.AddHostedService<FactoryConversion>();

        return builder;
    }

    /// <summary>World's messaging with the analysis service (ADR-0018), for the host's <c>UseWolverine</c>.</summary>
    public static Wolverine.WolverineOptions AddWorldMessaging(this Wolverine.WolverineOptions options) =>
        AnalysisMessages.AddWorldMessaging(options);

    public static IEndpointRouteBuilder MapWorldEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/v1/world")
            .WithTags("World")
            .MapSnapshotEndpoints()
            .MapFactoryEndpoints()
            .MapAnalysisEndpoints()
            .MapWorldMapEndpoints();

        // For the analysis service only (ADR-0018): not part of the public API or its OpenAPI document.
        endpoints.MapGroup("/internal/world")
            .ExcludeFromDescription()
            .MapInternalAnalysisEndpoints();

        return endpoints;
    }

    /// <summary>Creates or updates the world schema. Used at startup in development and by the integration tests.</summary>
    public static async Task MigrateWorldDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<WorldDbContext>().Database.MigrateAsync(cancellationToken);
    }
}
