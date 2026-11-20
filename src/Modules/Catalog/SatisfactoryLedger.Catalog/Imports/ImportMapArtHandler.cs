using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using SatisfactoryLedger.Catalog.Domain;
using SatisfactoryLedger.Catalog.Domain.Import;
using SatisfactoryLedger.Catalog.Domain.Map;
using SatisfactoryLedger.Catalog.GameData.MapArtFile;
using SatisfactoryLedger.Catalog.Imports.Dto.Commands;
using SatisfactoryLedger.Catalog.Imports.Exceptions;
using SatisfactoryLedger.Catalog.Persistence;

namespace SatisfactoryLedger.Catalog.Imports;

/// <summary>
/// Imports the map art the map tool writes (ADR-0020): read it whole, then replace the stored map art with it, in one
/// transaction. A file that can't be read changes nothing.
/// </summary>
internal sealed class ImportMapArtHandler(CatalogDbContext db, TimeProvider clock)
{
    public async Task<CatalogImport> HandleAsync(ImportMapArtCommand command, CancellationToken cancellationToken)
    {
        MapArt art = await ReadAsync(command.File, cancellationToken);
        var import = CatalogImport.RecordMapArt(CatalogImportId.New(), art.GameVersion, clock.GetUtcNow());

        // Aspire's Npgsql retries a failed command, and a retry must repeat the whole transaction: so the transaction
        // runs inside the execution strategy, not around it.
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            // One map art at a time; its tiles go with it (the foreign key cascades).
            await db.MapArts.ExecuteDeleteAsync(cancellationToken);
            db.MapArts.Add(art);
            db.Imports.Add(import);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
        return import;
    }

    private static async Task<MapArt> ReadAsync(Stream file, CancellationToken cancellationToken)
    {
        try
        {
            return await MapArtReader.ReadAsync(file, cancellationToken);
        }
        catch (Exception e) when (e is JsonException or InvalidDataException or ArgumentException)
        {
            throw new InvalidMapArtException(e.Message, e);
        }
    }
}
