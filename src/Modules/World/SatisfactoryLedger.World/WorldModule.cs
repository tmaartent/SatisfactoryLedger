using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using SatisfactoryLedger.World.Factories;
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

        // Adds Aspire's extras to that DbContext: retries on transient errors, a health check, tracing and metrics.
        builder.EnrichNpgsqlDbContext<WorldDbContext>();

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddScoped<ImportSaveHandler>();
        builder.Services.AddScoped<DefineFactoryHandler>();

        return builder;
    }

    public static IEndpointRouteBuilder MapWorldEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/v1/world")
            .WithTags("World")
            .MapSnapshotEndpoints()
            .MapFactoryEndpoints();

        return endpoints;
    }

    /// <summary>Creates or updates the world schema. Used at startup in development and by the integration tests.</summary>
    public static async Task MigrateWorldDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<WorldDbContext>().Database.MigrateAsync(cancellationToken);
    }
}
