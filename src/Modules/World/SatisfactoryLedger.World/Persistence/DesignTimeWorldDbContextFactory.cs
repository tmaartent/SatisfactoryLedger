using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SatisfactoryLedger.World.Persistence;

/// <summary>
/// Used only by the `dotnet ef` tool to build the model when creating a migration. Creating a migration
/// doesn't connect to a database, so no real connection string is needed.
/// </summary>
internal sealed class DesignTimeWorldDbContextFactory : IDesignTimeDbContextFactory<WorldDbContext>
{
    public WorldDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<WorldDbContext>();
        WorldDbContext.Configure(options, "Host=localhost;Database=design-time");
        return new WorldDbContext(options.Options);
    }
}
