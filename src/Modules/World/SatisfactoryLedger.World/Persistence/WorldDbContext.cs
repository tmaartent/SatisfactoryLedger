using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SatisfactoryLedger.World.Analysis;
using SatisfactoryLedger.World.Factories;
using SatisfactoryLedger.World.Snapshots;

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

/// <summary>
/// One imported save, as stored (ADR-0016): columns for what lists need, and the whole content as one jsonb document.
/// A persistence record, not a domain entity: the domain's <c>WorldGraph</c> is built from <see cref="Content"/> when needed.
/// </summary>
internal sealed class StoredSnapshot
{
    public required Guid Id { get; init; }

    public required string SessionName { get; init; }

    public required int GameBuild { get; init; }

    public required DateTimeOffset SavedAt { get; init; }

    public required DateTimeOffset ImportedAt { get; init; }

    public required SnapshotCounts Counts { get; init; }

    public required List<string> UnknownRecipes { get; init; }

    public required List<UnsupportedDocument> Unsupported { get; init; }

    /// <summary>The <see cref="SnapshotDocument"/> as JSON.</summary>
    public required string Content { get; init; }
}

internal sealed class StoredSnapshotConfiguration : IEntityTypeConfiguration<StoredSnapshot>
{
    public void Configure(EntityTypeBuilder<StoredSnapshot> snapshot)
    {
        snapshot.ToTable("snapshots");
        snapshot.HasKey(s => s.Id);
        snapshot.Property(s => s.SessionName);
        snapshot.Property(s => s.GameBuild);
        snapshot.Property(s => s.SavedAt);
        snapshot.Property(s => s.ImportedAt);
        snapshot.HasIndex(s => s.ImportedAt);

        // Value objects without identity: their fields become columns (counts_machines, ...).
        snapshot.ComplexProperty(s => s.Counts);
        // A list of strings becomes a PostgreSQL text[] column.
        snapshot.PrimitiveCollection(s => s.UnknownRecipes);
        snapshot.ComplexCollection(s => s.Unsupported, unsupported => unsupported.ToJson("unsupported"));

        snapshot.Property(s => s.Content).HasColumnType("jsonb");
    }
}
