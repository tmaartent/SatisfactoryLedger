using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SatisfactoryLedger.Catalog.Persistence;

using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(SatisfactoryLedger.Api.IntegrationTests.LedgerApiFactory))]

namespace SatisfactoryLedger.Api.IntegrationTests;

/// <summary>
/// The real API in memory, on a real PostgreSQL in a container: one of each for the whole test run.
/// Program.cs migrates the database at startup (the test host runs as Development).
/// </summary>
public sealed class LedgerApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:ledger", _postgres.GetConnectionString());
    }

    /// <summary>Empties the catalog so each test starts from nothing.</summary>
    public async Task ResetCatalogAsync()
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE catalog.items, catalog.buildings, catalog.transport_tiers, catalog.recipes, catalog.imports");
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}