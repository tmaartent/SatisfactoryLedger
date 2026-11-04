# Step 05 · Catalog: lifts and extractors

**Goal:** give the World what it needs from the Catalog to interpret a save. Lift capacities (your save has about 5,600 Mk.6 lifts), and what each extractor delivers.
**Result:** the Catalog imports 6 lifts and the 2 clean pipelines as transport tiers, and 6 extractors as a new kind of catalog entry, browsable at `GET /api/v1/catalog/extractors`. The full solution build is green with 191 tests passing, and the OpenAPI document gained `extractors` on the import response. **The 16 API integration tests didn't run locally because Docker wasn't running.** This step changes the database and the API, so they need a run before this is committed (see the end).
**Decisions you made:** do lifts and extractors now. The Docs file is from 1.2.
**Decisions I made, for you to overrule:** see "Choices worth a second look".

## What was built

| Layer | Change |
|---|---|
| Domain | `Extractor` (id, name, form, amount per cycle, cycle seconds) with `ExtractionRate`, computed exactly with `Rate.FromRecipe`. `ExtractorId`. `CatalogImport` counts and lists extractors. |
| Importer | `LiftGroupDto` (`FGBuildableConveyorLift`). `ExtractorDto` and `ExtractorGroupDto`, with one subclass per game section: `FGBuildableResourceExtractor` (miners, oil), `FGBuildableWaterPump`, `FGBuildableFrackingExtractor`. |
| Translator | Lifts become belt-form tiers. `TranslateToExtractors`: the form comes from `mAllowedResourceForms`, and fluid amounts are divided by 1000, as for recipes. |
| Use case | `ImportCatalogHandler` reconciles extractors like the other entries (ADR-0009). |
| Persistence | `extractors` table, extractor counts on `imports`, migration `AddExtractors`. |
| API | `extractors` counts on the import response. New `GET /extractors`: id, name, form, amount per cycle, cycle seconds, `extractionRate` (`perMinute` plus `exact`, like recipes) and `inGame`, sorted by name. `/transport-tiers` lists lifts and clean pipelines too, sorted by form, capacity and then name, so tiers with the same capacity have a fixed order. |
| Importer, clean pipes | The filter that dropped `…NoIndicator_C` pipes is gone (added on your request). "Clean Pipeline Mk.1/Mk.2" are pipe tiers at 300 and 600, so every pipe class in a save resolves. |

What the game file gives, per extractor (normal node, 100%):

| Extractor | Per cycle | Cycle | Per minute |
|---|---|---|---|
| Miner Mk.1 / Mk.2 / Mk.3 | 1 | 1 s / 0.5 s / 0.25 s | 60 / 120 / 240 |
| Oil extractor | 2 m³ (2000 in the file) | 1 s | 120 m³ |
| Water extractor | 2 m³ | 1 s | 120 m³ |
| Resource well extractor | 1 m³ | 1 s | 60 m³ |

## Tests (written first)

- `ExtractorTests` (11): the rates above, validation, equality by id, updating from the game, no longer in game.
- `CatalogImportTests`: an import counts and lists the extractors it changed.
- `SatisfactoryTranslatorTests`, against the real game file:
  - lifts carry as much as belts of the same mark,
  - which six buildings are extractors,
  - their rates and forms (a theory with five cases),
  - the counts updated to 14 tiers and 6 extractors.
- `SatisfactoryImporterTests`: the lift section, and the extractors in their three sections.
- `SatisfactoryTranslatorTests`: clean pipelines carry as much as pipelines of the same mark (a theory with two cases). The importer tests now expect the clean pipes instead of asserting they're dropped.
- Integration tests, updated: 16 tiers and 6 extractors on import, a re-import adds no extractors, the tier order includes lifts and clean pipes, and `GET /extractors` lists all six with their rates.

## .NET points worth noticing

| .NET | Java/Spring (TypeScript) | Note |
|---|---|---|
| `[JsonDerivedType]` registers a type once | Jackson `@JsonSubTypes.Type(names = {...})` | One concept in three game sections means three tiny subclasses. In the primer. |
| `ArgumentOutOfRangeException.ThrowIfNegativeOrZero` | Guava `checkArgument(x > 0)` | Guard helpers on the exception types, like `ArgumentException.ThrowIfNullOrWhiteSpace`. |
| `dotnet ef migrations add` on a `ComplexProperty` | Flyway script by hand | A new value object on `imports` became three columns with default 0, so existing rows stay valid. |

## Choices worth a second look

1. **Lifts are belt-form tiers**, not a third form. They carry solids at belt speed, and everything that asks "what can a belt carry" should count lifts too. The API's `form` enum stays `belt | pipe`.
2. **The extraction rate is per normal-purity node at 100%.** Purity isn't in the Docs file. It belongs to the node in the world, so the World applies it (impure ×0.5, pure ×2) together with the clock speed. Where the save stores a node's purity is still to be checked.
3. **The resource well pressurizer is not an extractor.** It powers the well's satellite nodes, and the extractors on those nodes deliver the fluid.
4. **`GET /extractors` has no filter.** There are only six; `?form=` can be added like on `/items` when needed.
5. **Clean pipelines are separate tiers,** not aliases of the normal pipeline. Each class in the save maps to exactly one tier, and the list shows both names. Your save has 225 of them, probably from blueprints you didn't design yourself.

## Before committing

The API integration tests start PostgreSQL with Testcontainers and need Docker. Start Docker Desktop and run:

```bash
dotnet test --project tests/SatisfactoryLedger.Api.IntegrationTests
```

Or push, and CI runs them.
