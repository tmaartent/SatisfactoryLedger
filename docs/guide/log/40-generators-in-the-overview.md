# Step 40 · Generators in the overview

**Goal:** a factory of generators only showed nothing in its overview. *Rocket Fuel Power Plant* (factory `01a1cbf1…`, reported 2026-11-11) has 360 Fuel-Powered Generators on Rocket Fuel, but the summary cards said 0 machines, 0 at 100%, 0 starved, and its Output table was empty. Branch `feat/choose-factory-in-3d`, on top of step 39.

## What you decided (2026-11-11)

- **A second row of cards for generators**: generators · at 100% · starved · output blocked · fuel unknown. Shown only when there are generators, on the factory page and on the whole snapshot's findings. The machine row stays as it was. BR-A15.
- **"Nothing leaves this factory."** in place of an empty Output table. With generators, the Power section says what it makes.

## How it works

- `AnalysisSummary` counted the machines' findings only; generators have their own findings (BR-A15). The new `GeneratorSummary` counts the generator findings by their limit, plus the generators whose fuel isn't known. Those aren't simulated, so they're not among the findings: they're counted from the snapshot (a consumer with a known generator class and no fuel), in the factory or the whole world.
- `AnalysisResponse` and `FactoryAnalysisResponse` carry it next to `Summary`. The web's `Findings` type picks it up, so the snapshot page and the factory page both show the row.

On your world, in the model: Rocket Fuel Power Plant 360 generators, 300 at 100%, 60 starved; the whole world 429 generators, 357 at 100%, 70 starved, 2 output blocked. Checked through the running app.

## Files

| Where | What |
|---|---|
| `World/Analysis/GeneratorSummary.cs` (new) | Generators, running, starved, output blocked, fuel unknown |
| `World/Analysis/AnalysisResponse.cs`, `FactoryAnalysisResponse.cs` | `GeneratorSummary` |
| `World/Analysis/SnapshotAnalysis.cs` | Counts it in `Findings` |
| `web/src/app/world/findings.*` | The generator row; `Findings` picks `generatorSummary` |
| `web/src/app/world/factory-page.html` | *Nothing leaves this factory.* |
| `api/openapi.json` | `GeneratorSummary` |
| BR-A15 | The row |

## Tests

Application (`GeneratorFindingsTests`): the generator summary counts its generators by how they run (the test save's nuclear plant and coal generator run, its fuel generator starves: 3, 2, 1, 0, 0) · a generator whose fuel isn't known counts as fuel unknown (a hand-built world with an empty Fuel-Powered Generator).

Web (`findings.spec.ts`): the generators are counted in a row of their own, next to the machines · no generator row without generators.

**Totals:** 895 .NET tests, 78 web tests.

## Points worth noticing

| | Java / TypeScript | Here |
|---|---|---|
| Adding a field to a positional record | A new constructor parameter, every `new` call updated | The same: `new AnalysisResponse(…)` has one caller, and the compiler finds it. A record's positional parameters are its constructor, so there's no default to forget |
| `textContent` in a component test | Same in the DOM | Cards next to each other have no space between them in `textContent` (`360generators300at 100%`): the test compares card by card |

## Choices worth a second look

1. **The world's overview shows the row too**, as the factory page does: it's the same `Findings` view.
2. **Fuel unknown is counted from the snapshot**, not from the findings, as such a generator isn't simulated. A consumer counts as a generator when the Catalog knows its class (`IsMachineOrGenerator`).
3. **The empty-output line is the same for any factory**, generators or not.

## Next

Part 2b(2): a live preview of the traced factory while choosing in 3D.
