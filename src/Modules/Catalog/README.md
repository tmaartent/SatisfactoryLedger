# Catalog module

The game's facts, as the game's Docs file (`en-US.json`) states them: items, recipes, buildings, belt, lift and pipe tiers, extractors, generators, the drone port, building footprints, and the resource nodes of the map. It's reference data: read-mostly, and it changes only when you import the Docs file of a new game version.

It's a *supporting* subdomain ([context map](../../../docs/domain/context-map.md)). The World module, the core, asks it for the rates and sizes it needs.

## Projects

| Project | What's in it | May reference |
|---|---|---|
| `SatisfactoryLedger.Catalog.Domain` | The entries (`Item`, `Recipe`, `Building`, `TransportTier`, `Extractor`, `Generator`, `DronePort`, `Footprint`, `ResourceNode`), each with its id; `CatalogEntries.Reconcile`, which brings stored entries in line with a new file; `CatalogImport`, the record of one import. No framework references. | SharedKernel |
| `SatisfactoryLedger.Catalog` | Everything around the domain: the importer and translator (the anti-corruption layer for the game's format), the import and browse endpoints, persistence, and the queries other modules call. | its own Domain and Contracts |
| `SatisfactoryLedger.Catalog.Contracts` | What other modules may use: `ICatalogQueries` and the plain records it returns (`CatalogRecipe`, `CatalogGenerator`, `CatalogDronePort`, …). | nothing |

Other modules reference **only** `Catalog.Contracts` (ADR-0003). The architecture tests enforce it.

## Inside `SatisfactoryLedger.Catalog`

| Folder | What it does |
|---|---|
| `Importer/` | Reads the Docs file: UTF-16 JSON, one section per game class (`NativeClass`), into DTOs. Sections the Catalog doesn't import are dropped first. |
| `Translator/` | Turns those DTOs into domain entries. The game's quirks stop here: class names like `Desc_IronPlate_C`, fluid amounts in litres (the Catalog keeps cubic metres), decimals written as `"6.000000"`, clearance boxes as one long string. |
| `Imports/` | `POST /api/v1/catalog/imports` (the Docs file) and `POST /api/v1/catalog/resource-node-imports` (the node table the map tool writes, ADR-0015). Re-importing the same file changes nothing (ADR-0009). |
| `Browsing/` | `GET /api/v1/catalog/items`, `recipes`, `transport-tiers`, `extractors`, `resource-nodes`: what the web app shows. |
| `Queries/` | `CatalogQueries`, the implementation of `ICatalogQueries` that the World module calls when it imports a save. |
| `Persistence/` | EF Core: `CatalogDbContext` in schema `catalog`, one configuration per entry, and the migrations. |

## How the data flows

```
Docs file (en-US.json) → Importer → Translator → domain entries → Reconcile with what's stored → PostgreSQL (schema catalog)
                                                                                                   ↓
                                                            World module ← ICatalogQueries (Catalog.Contracts)
```

An entry that's no longer in the game file is marked *no longer in game*, never deleted, so snapshots that refer to it stay readable (ADR-0009).

## Worth reading

- [ADR-0005](../../../docs/adr/0005-game-data-import.md): importing the game data.
- [ADR-0009](../../../docs/adr/0009-catalog-reimport-upsert-and-mark.md): re-import as upsert-and-mark.
- [ADR-0011](../../../docs/adr/0011-catalog-handlers-use-dbcontext.md): handlers use the `DbContext` directly.
- [ADR-0015](../../../docs/adr/0015-resource-nodes-from-the-game-map.md): resource nodes from the game map.
- Tests: `tests/SatisfactoryLedger.Catalog.Domain.Tests` (the entries), `tests/SatisfactoryLedger.Catalog.Tests` (importer and translator, on the real Docs file), and `tests/SatisfactoryLedger.Api.IntegrationTests/Catalog`.
