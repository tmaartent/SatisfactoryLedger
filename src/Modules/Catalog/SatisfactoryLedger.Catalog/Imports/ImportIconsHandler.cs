using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using SatisfactoryLedger.Catalog.Domain;
using SatisfactoryLedger.Catalog.Domain.Icons;
using SatisfactoryLedger.Catalog.Domain.Import;
using SatisfactoryLedger.Catalog.GameData.IconFile;
using SatisfactoryLedger.Catalog.Imports.Dto.Commands;
using SatisfactoryLedger.Catalog.Imports.Exceptions;
using SatisfactoryLedger.Catalog.Persistence;

namespace SatisfactoryLedger.Catalog.Imports;

/// <summary>
/// Imports the icons the map tool writes (ADR-0032): read them whole, then replace the stored icon set with them, in
/// one transaction. A file that can't be read changes nothing.
/// </summary>
internal sealed class ImportIconsHandler(CatalogDbContext db, TimeProvider clock)
{
    public async Task<CatalogImport> HandleAsync(ImportIconsCommand command, CancellationToken cancellationToken)
    {
        IconSet icons = await ReadAsync(command.File, cancellationToken);
        var import = CatalogImport.RecordIcons(CatalogImportId.New(), icons.GameVersion, clock.GetUtcNow());

        // As the map art's: the transaction runs inside the execution strategy, so a retry repeats all of it.
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            // One icon set at a time; its icons go with it (the foreign key cascades).
            await db.IconSets.ExecuteDeleteAsync(cancellationToken);
            db.IconSets.Add(icons);
            db.Imports.Add(import);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
        return import;
    }

    private static async Task<IconSet> ReadAsync(Stream file, CancellationToken cancellationToken)
    {
        try
        {
            return await IconFileReader.ReadAsync(file, cancellationToken);
        }
        catch (Exception e) when (e is JsonException or InvalidDataException or ArgumentException)
        {
            throw new InvalidIconsException(e.Message, e);
        }
    }
}
