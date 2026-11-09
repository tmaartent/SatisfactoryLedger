using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using SatisfactoryLedger.Catalog.Domain;
using SatisfactoryLedger.Catalog.Domain.GameObjects;
using SatisfactoryLedger.Catalog.Domain.Import;
using SatisfactoryLedger.Catalog.GameData.NodeTable;
using SatisfactoryLedger.Catalog.Imports.Dto.Commands;
using SatisfactoryLedger.Catalog.Imports.Exceptions;
using SatisfactoryLedger.Catalog.Persistence;

namespace SatisfactoryLedger.Catalog.Imports;

/// <summary>
/// Imports the node table the map tool writes (ADR-0015): read it, reconcile with the stored nodes (ADR-0009), save.
/// The game version comes from the table itself. Importing the same table again changes nothing.
/// </summary>
internal sealed class ImportResourceNodesHandler(CatalogDbContext db, TimeProvider clock)
{
    public async Task<CatalogImport> HandleAsync(ImportResourceNodesCommand command, CancellationToken cancellationToken)
    {
        ResourceNodeTable table = await ReadAsync(command.Table, cancellationToken);

        // Loaded entities are tracked: the changes Reconcile makes to them are saved by SaveChangesAsync.
        var nodes = CatalogEntries.Reconcile(await db.ResourceNodes.ToListAsync(cancellationToken), table.Nodes);
        db.ResourceNodes.AddRange(nodes.Added);

        var import = CatalogImport.RecordResourceNodes(CatalogImportId.New(), table.GameVersion, clock.GetUtcNow(), nodes);
        db.Imports.Add(import);

        await db.SaveChangesAsync(cancellationToken);
        return import;
    }

    private static async Task<ResourceNodeTable> ReadAsync(Stream table, CancellationToken cancellationToken)
    {
        try
        {
            return await ResourceNodeTableReader.ReadAsync(table, cancellationToken);
        }
        catch (Exception e) when (e is JsonException or InvalidDataException or ArgumentException)
        {
            throw new InvalidResourceNodeTableException(e.Message, e);
        }
    }
}