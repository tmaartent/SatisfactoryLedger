using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SatisfactoryLedger.AnalysisService;

using SatisfactoryLedger.Catalog.Persistence;
using SatisfactoryLedger.World.Persistence;

using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

[assembly: AssemblyFixture(typeof(SatisfactoryLedger.Api.IntegrationTests.LedgerApiFactory))]

namespace SatisfactoryLedger.Api.IntegrationTests;

/// <summary>
/// The real API in memory, on a real PostgreSQL and RabbitMQ in containers, with the analysis service running next to
/// it (ADR-0018): one of each for the whole test run. Program.cs migrates the database at startup (the test host runs
/// as Development). The service reaches the API through the in-memory test server instead of the network.
/// </summary>
public sealed class LedgerApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly RabbitMqContainer _rabbitmq = new RabbitMqBuilder("rabbitmq:4-alpine").Build();
    private IHost? _analysisService;

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitmq.StartAsync());

        // The API first (Server starts it), then the service, which calls it.
        Uri api = Server.BaseAddress;
        HostApplicationBuilder service = Host.CreateApplicationBuilder();
        service.Configuration["ConnectionStrings:rabbitmq"] = _rabbitmq.GetConnectionString();
        service.AddAnalysisService(world => world
            .ConfigurePrimaryHttpMessageHandler(() => Server.CreateHandler())
            .ConfigureHttpClient(client => client.BaseAddress = api));
        _analysisService = service.Build();
        await _analysisService.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:ledger", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:rabbitmq", _rabbitmq.GetConnectionString());
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
        if (_analysisService is not null)
        {
            await _analysisService.StopAsync();
            _analysisService.Dispose();
        }

        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        await _rabbitmq.DisposeAsync();
    }
}