# Step 11 · Catalog: importing the resource node table

**Goal:** the second half of [ADR-0015](../../adr/0015-resource-nodes-from-the-game-map.md). The Catalog imports the node table the map tool writes, so the World can look up what each extractor stands on.
**Result:** `POST /api/v1/catalog/resource-node-imports` imports the 577 nodes from your install, and a re-import changes nothing. `GET /api/v1/catalog/resource-nodes?resource=…` browses them. `ICatalogQueries.GetResourceNodesAsync` answers the World. 23 new tests, all green. Solution total: 375 green, including the integration tests on Docker.
**Decisions you made:** do this now.
**Decisions I made, for you to overrule:** see "Choices worth a second look".

## What was built

```
POST /api/v1/catalog/resource-node-imports   multipart: file (resource-nodes.json) → 201 + Location, 400, 422
GET  /api/v1/catalog/resource-nodes          ?resource=Desc_OreIron_C → name, kind, resource, purity, position, inGame
GET  /api/v1/catalog/imports/{id}            now also says the source (docsFile or resourceNodeTable) and the node counts
```

| Layer | Change |
|---|---|
| Domain | `ResourceNode` (an `ICatalogEntry`, equal by name), `ResourceNodeId`, `ResourceNodeKind`, `Purity`, `MapPosition`. `CatalogImport` gets a `Source` and `ResourceNodes` counts, and a second factory, `RecordResourceNodes`. |
| Importer | `ResourceNodeTableReader`: our own table format, version 1. An unknown version, kind or purity, or a node without a position, is an `InvalidDataException`. |
| Use case | `ImportResourceNodesHandler`: read, reconcile with the stored nodes (ADR-0009), record, save. The game version comes from the table. |
| API | The import endpoint (422 with code `Catalog.InvalidResourceNodeTable`), the browse endpoint, and `source` and `resourceNodes` on the import response. |
| Contracts | `GetResourceNodesAsync(names)` → `CatalogResourceNode(Name, Kind, Resource, Purity)`, with kind and purity as strings, as the API writes them. |
| Persistence | Table `resource_nodes` (position as three columns, an index on the resource). `source` and node counts on `imports`. Migration `AddResourceNodes`. |
| Fixture | `tests/SatisfactoryLedger.Catalog.Tests/Fixtures/resource-nodes.json`: the table the map tool wrote from your 1.2 install. |

## How

- **One import history for both sources.** A node import is a `CatalogImport` like a Docs import, with `Source = ResourceNodeTable` and only node counts. `GET /imports/{id}` shows either kind. The alternative, a separate import record per source, would mean a second table and endpoint for the same idea.
- **Reconciling works as for every other entry.** Nodes missing from a newer table are marked no longer in the game, not deleted. A save that still points at one can still find it.
- **A hand edit in the generated migration.** The new `source` column needs a value for the imports already stored. EF chose `""`; I changed it to `"DocsFile"`, because every earlier import read the Docs file. The migration has a comment saying so.
- **The table reset in the integration tests picked up the new table on its own.** Since step 05 it's read from EF's model, so nothing needed changing there.

## Tests

- **Domain:** `ResourceNodeTests` (7) and 2 new `CatalogImportTests`. They cover the source and node counts.
- **Reader:** `ResourceNodeTableReaderTests` (9), against your table and against small JSON written in the tests:
  - 459 nodes and 118 satellites;
  - `BP_ResourceNode35` is normal iron ore;
  - each purity;
  - refusals: a wrong version, an unknown purity, a missing position, a file that isn't JSON.
- **API:** `ResourceNodeImportApiTests` (4):
  - all 577 nodes added;
  - a re-import updates 577 and adds or removes none;
  - 127 iron nodes when browsing;
  - 422 for a wrong file.

  Plus a source check on the Docs import, and `GetResourceNodesAsync`, which leaves out a water volume.

## .NET points worth noticing

| .NET | Java/Spring (TypeScript) | Note |
|---|---|---|
| `$$"""{ "version": {{version}} }"""` | Java text block + `formatted()` | An interpolated raw string with two `$`: single braces stay braces. In the primer. |
| Two static factories on one entity (`Record`, `RecordResourceNodes`) | Named constructors / static factories | Each says which kind of import it is, instead of one factory with many optional arguments. |
| Editing a generated migration | Editing a Flyway script | Allowed, and sometimes needed for data. Say why in a comment. |

## Choices worth a second look

1. **A node's resource isn't checked against the Catalog's items.** The table comes from the same game as the Docs file, so a mismatch means the two are from different versions. Checking would force the Docs import to run first, every time.
2. **Positions are required.** The tool writes one for every node; a table without them is treated as broken.
3. **Kind and purity in the contract are strings** (`"resourceNode"`, `"impure"`): contracts may not depend on the Catalog's domain types. The World turns purity into BR-W9's multiplier.
4. **The World doesn't use the nodes yet.** At save import it should copy each provider's node (resource and purity) into the snapshot, as it does with recipes (ADR-0016). That fits with the flow analysis, which is the first thing to need it.
