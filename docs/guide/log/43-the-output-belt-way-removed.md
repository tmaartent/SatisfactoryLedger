# Step 43 · The output belt way removed

**Goal:** remove the old way of defining a factory, by its output belt and input items (BR-W12), unused since the 3D selection of step 37. One of the loose ends you chose to clear first (2026-11-12). Branch: `main`, uncommitted.

## Before removing: a check

The startup conversion (`FactoryConversion`) turned factories stored the old way into their machines, and kept one it couldn't trace as it was, to try again at the next start. Dropping the columns would have lost such a factory. A read-only query on your database (with your go-ahead) found **72 factories, none with an output belt or input items left, none without machines**.

## What was removed

| Layer | Removed |
|---|---|
| API | `outputBelt` and `inputItems` on `POST /factories` and in its response; the problem codes `World.NotAnOutputBelt`, `World.UnknownInputItems`; `GET /snapshots/{id}/output-belts` |
| Application | `OutputBeltSuggestions`, `OutputBeltSuggestion`, `FactoryConversion` (the startup background service), `StoredFactory.OutputBelt`, `InputItems`, `ConvertIn`; the Catalog lookup in `DefineFactoryHandler` (it only checked input items) |
| Domain | `CandidateOutputBelts`, `OutputBeltCandidate`, `OutputBeltTrace` |
| Database | the columns `output_belt` and `input_items` (migration `DropFactoryOutputBelt`) |
| Web | `outputBelt: null, inputItems: null` in the 3D define request; the generated `getOutputBelts` and `OutputBeltSuggestion` |

`POST /factories` now asks for `machines` only: *"Give the ids of the factory's machines."*

On your world, after the migration ran at startup: the `factories` table has `id`, `machines`, `name` and `world`; all 72 factories are there and all trace; `GET output-belts` is 404. Checked through the running app.

## Tests

Removed with the feature (34): the domain tests of candidate output belts and the output belt trace; the API tests of defining by output belt, converting old factories, the suggestions, the two problem codes, and "machines or an output belt, not both"; the application tests of converting and suggesting. Two tests used the old trace only to show a factory can be traced: one in `TestWorldFactoryTests` went (the factory-by-machines tests above it cover that), the one in `SnapshotDocumentTests` now traces the three machines and checks the factory owns the belt into its container.

**Totals:** 884 .NET tests, 79 web tests.

## Points worth noticing

| | Java / Spring | Here |
|---|---|---|
| Dropping a column | A Flyway script by hand | `dotnet ef migrations add DropFactoryOutputBelt` generates `DropColumn` from the model, with a warning that data may be lost, and a `Down` that adds them back (empty) |
| A background job at startup | `ApplicationRunner` / `@EventListener(ApplicationReadyEvent)` | `BackgroundService` registered with `AddHostedService`; removing it is removing the registration in `WorldModule` |

## Docs

BR-W2 (the conversion is done and removed), BR-W12 superseded, glossary (*Candidate Output Belt*, *Output Belt*), ADR-0025 (an update section). The possible enhancement *A factory drawn as a rectangle on the map*, which would have proposed an output belt and input items, is dropped (you chose, 2026-11-12): the 3D selection does that job.

## Next

The other loose end: a factory's generators edged in its colour on the map.
