using Microsoft.EntityFrameworkCore;

using SatisfactoryLedger.World.Analysis;
using SatisfactoryLedger.World.Factories;

namespace SatisfactoryLedger.World.Persistence;

/// <summary>The World module's own database context, in its own schema (ADR-0007).</summary>
internal sealed class WorldDbContext(DbContextOptions<WorldDbContext> options) : DbContext(options)
{
    public const string Schema = "world";

    /// <summary>The connection string name the AppHost gives the database (see AppHost.cs). One database, a schema per module.</summary>
    public const string ConnectionStringName = "ledger";

    public DbSet<StoredSnapshot> Snapshots => Set<StoredSnapshot>();

    public DbSet<StoredFactory> Factories => Set<StoredFactory>();

    public DbSet<StoredAnalysis> Analyses => Set<StoredAnalysis>();

    /// <summary>The PostgreSQL settings, shared by the app and the design-time factory used by `dotnet ef`.</summary>
    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder options, string? connectionString)
    {
        return options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", Schema))
            .UseSnakeCaseNamingConvention();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorldDbContext).Assembly);
    }
}