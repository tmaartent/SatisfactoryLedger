using Microsoft.EntityFrameworkCore;

using SatisfactoryLedger.Catalog.Domain;

namespace SatisfactoryLedger.Catalog.Persistence;

/// <summary>The Catalog module's own database context, in its own schema (ADR-0007).</summary>
internal sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public const string Schema = "catalog";

    /// <summary>The connection string name the AppHost gives the database (see AppHost.cs).</summary>
    public const string ConnectionStringName = "ledger";

    public DbSet<Item> Items => Set<Item>();

    public DbSet<Building> Buildings => Set<Building>();

    public DbSet<TransportTier> TransportTiers => Set<TransportTier>();

    public DbSet<Recipe> Recipes => Set<Recipe>();

    public DbSet<Extractor> Extractors => Set<Extractor>();

    public DbSet<Footprint> Footprints => Set<Footprint>();

    public DbSet<ResourceNode> ResourceNodes => Set<ResourceNode>();

    public DbSet<CatalogImport> Imports => Set<CatalogImport>();

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
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
    }
}