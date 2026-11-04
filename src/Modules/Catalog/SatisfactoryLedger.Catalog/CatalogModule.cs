using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using SatisfactoryLedger.Catalog.Browsing;
using SatisfactoryLedger.Catalog.Imports;
using SatisfactoryLedger.Catalog.Persistence;

namespace SatisfactoryLedger.Catalog;

/// <summary>The Catalog module's only public type: the host uses it to register and map the module (ADR-0003).</summary>
public static class CatalogModule
{
    public static IHostApplicationBuilder AddCatalogModule(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContext<CatalogDbContext>(options =>
            CatalogDbContext.Configure(options, builder.Configuration.GetConnectionString(CatalogDbContext.ConnectionStringName)));

        // Adds Aspire's extras to that DbContext: retries on transient errors, a health check, tracing and metrics.
        builder.EnrichNpgsqlDbContext<CatalogDbContext>();

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddScoped<ImportCatalogHandler>();

        return builder;
    }

    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/v1/catalog")
            .WithTags("Catalog")
            .MapImportEndpoints()
            .MapBrowseEndpoints();

        return endpoints;
    }

    /// <summary>Creates or updates the catalog schema. Used at startup in development and by the integration tests.</summary>
    public static async Task MigrateCatalogDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.MigrateAsync(cancellationToken);
    }
}