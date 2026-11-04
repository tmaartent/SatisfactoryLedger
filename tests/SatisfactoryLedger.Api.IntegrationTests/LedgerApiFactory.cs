using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SatisfactoryLedger.Catalog.Persistence;
using SatisfactoryLedger.World.Persistence;

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

    /// <summary>
    /// Empties every module's tables so each test starts from nothing. The tables come from each DbContext's model,
    /// not a hand-written list, so a new entity can't be forgotten here (the extractors table once was).
    /// </summary>
    public async Task ResetAsync()
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        await TruncateAsync(scope.ServiceProvider.GetRequiredService<CatalogDbContext>());
        await TruncateAsync(scope.ServiceProvider.GetRequiredService<WorldDbContext>());
    }

    private static async Task TruncateAsync(DbContext db)
    {
        string tables = string.Join(", ", db.Model.GetEntityTypes()
            .Select(entity => $"\"{entity.GetSchema()}\".\"{entity.GetTableName()}\"")
            .Distinct());
        // Table names can't be SQL parameters. These come from EF's model, not from input, so raw SQL is safe here.
        string truncate = "TRUNCATE " + tables;
        await db.Database.ExecuteSqlRawAsync(truncate);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}