using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SatisfactoryLedger.Catalog.Persistence;

/// <summary>
/// Used only by the `dotnet ef` tool to build the model when creating a migration. Creating a migration
/// doesn't connect to a database, so no real connection string is needed.
/// </summary>
internal sealed class DesignTimeCatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>();
        CatalogDbContext.Configure(options, "Host=localhost;Database=design-time");
        return new CatalogDbContext(options.Options);
    }
}