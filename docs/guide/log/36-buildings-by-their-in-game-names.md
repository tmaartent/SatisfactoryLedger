# Step 36 · Buildings by their in-game names

**Goal:** the UI shows every building by the name the game shows (decided 2026-11-10). Items already were. Buildings were their class names as words: "Train Docking Station" for a **Freight Platform**, "Central Storage" for the **Dimensional Depot Uploader**, "Conveyor Attachment Splitter Smart" for a **Smart Splitter**. The first, small part of the factory workflow's part 2b. Branch `feat/building-names`.

## How

The Catalog reads only some sections of the Docs file: production machines, belts, pipes, extractors, generators, drone ports, storages. Splitters, mergers, pipe junctions and sinks have no section it imports. But every buildable class has an `mDisplayName`, in whatever section it's in. The importer already collects footprints from every section (`FootprintsOf`), so display names are collected the same way.

| Where | What |
|---|---|
| `Catalog.Domain/GameObjects/Buildable.cs`, `BuildableId.cs` | A new Catalog entry: a class name and its name. Imported and reconciled like the others (*no longer in game* included) |
| `SatisfactoryImporter.BuildableNamesOf`, `BuildableNameGroupDto`, `BuildableNameDto` | Every `Build_` class with its display name, from every section |
| `SatisfactoryTranslator.TranslateToBuildables` | Into `Buildable`s |
| `ImportCatalogHandler`, `CatalogImport.Buildables`, `ImportResponse` | Reconciled and counted |
| `BuildableConfiguration`, migration `AddBuildables` | The `buildables` table, and three import count columns |
| `BrowseEndpoints`: `GET /api/v1/catalog/buildables` | Id, name, in game; by name |
| `web/src/app/shared/catalog-names.ts` | `CatalogNames.building()` uses them; one the Catalog doesn't know falls back to its class name as words, as before |

Every page that names a building already went through `CatalogNames.building()`: findings, the map's popups, the 3D view's card, the snapshot page, the catalog page and the define dialog. One change covers them all.

**Your Catalog needs the Docs file imported once more** to fill the new table. Until then the pages show the class names as words, as before.

## Tests

Written first:
- `BuildableTests`: known by the game's name; needs a name; a newer game file renames it.
- `SatisfactoryTranslatorTests`: Freight Platform, Smart Splitter, Dimensional Depot Uploader, Fluid Buffer, AWESOME Sink and Pipeline Junction, from the game's Docs file; only `Build_` classes.
- `CatalogImportTests`: an import counts the buildables it named.
- `CatalogBrowseApiTests`: the endpoint lists all 546, with the Freight Platform's name.
- `catalog-names.spec.ts` (web): a building by the game's name; an unknown one in words.

**Totals:** 849 .NET tests, 59 web tests.

## Points worth noticing

| | Java / TypeScript | Here |
|---|---|---|
| A second pass over raw JSON beside typed deserialisation | Jackson's `JsonNode` tree beside `readValue` | `JsonElement.EnumerateArray()` over the same `JsonDocument` the typed groups come from |
| `Promise.all` destructured into three | The same in TypeScript | `const [items, recipes, buildables] = await Promise.all([...])` in an Angular `resource` loader |
| One term in two modules | A shared class, or two with one name | *Buildable* in the World is one placed in the save; in the Catalog it's its kind, with the game's name. Same word, two sides of one idea, each in its own module (bounded context); the glossary says which is which |
