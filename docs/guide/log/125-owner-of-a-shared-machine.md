# Step 125 · Every page shows the oldest factory as owner of a shared machine

Card A of the architecture review of 2026-11-28 (`docs/architecture/reviews/2026-11-28.md`). BR-W5 already said who owns a machine two factories stored from before that rule: the oldest, "on every page and in every check, whatever order a page lists the factories in". The back end followed it; the web didn't. The trace response ordered factories by name and didn't say who owns what, so the web took the first by name. A fix, not a new rule (Q1).

## Design

- **Module:** `SnapshotFactories` stays the one module that decides ownership (`Owners`, by `StoredFactory.OldestFirst`). Its interface doesn't change; the trace response now carries what it decided.
- **Interface across the API seam (Q2, Q9):** each `MachineSummary` in `GET /snapshots/{id}/factories` has `owner`, the factory that owns it world-wide; the factories come oldest first, and the endpoint's summary says so. The order only gives the colours; who owns a machine is read from `owner`, never from the order.
- **Web (Q3):** `machineOwners` returns `Map<id, { owner, alsoIn }>` (`MachineOwner`), not a list whose first entry means "owner"; `alsoIn` is by name. Its callers read `.owner`; `factoriesOf(owner)` gives the owner first, then the others, for the area's panel and the draft's `blockedMachines`, which keeps its meaning (any other factory that has the machine blocks it, Q6). An owner the trace names but doesn't list is a bug in the back end, so `machineOwners` throws rather than guess (Q9: never from the order).
- **Colours (Q4):** still the position in the list `outlined` is given, but that list is now oldest first, so a factory defined later or renamed leaves the others' colours alone; only a deletion moves the ones after it.
- **Lists by name (Q5):** the legend (`factoriesInArea` in `factory-outlines.ts`), the map's factory list (`MapPage.factoryOptions`) and the MCP server's `list_factories` and "No factory is called …" (`FactoryTools.FactoriesByNameAsync`) sort by name themselves. The arrangement's `factoriesInArea` already did.
- **Deletion test:** nothing new to delete. What went is the web's second copy of the owner rule ("first by name"), which disagreed with the back end's.

## How it was built (`/tdd`, seams agreed in Q7)

1. **`FactoryTraces.TraceAll`**: two factories share a smelter, the younger first by name: both traces name the oldest as its owner. Red as a compile error (no `Owner`), then green. The first green run failed half the time: two `Guid.CreateVersion7()` in one millisecond have random bits after the timestamp, so "oldest" was a coin toss. The test now uses fixed ids, as `SnapshotFactoriesTests` does.
2. **`machineOwners`**: the owner is the one the world says, even when it comes later in the list, and `alsoIn` holds the other. Red (no `owner`, `alsoIn`), then green.
3. **Colours and the legend**: the legend lists by name whatever order it's given (red, then green). That a factory defined later keeps the others' colours was green from the start: the web already coloured by position; what changed is the order the API sends.

The typechecker then listed every caller that read `[0]` (Q3 made the change compile-checked). `tsc` doesn't check templates; `ng build` does, and `selectedOwners` needed its template's array.

**Tests changed:**
- `factory-outlines.spec.ts`: the owners tests describe one owner and `alsoIn`;
- `map-page.spec.ts`: the factory list is by name; three tests picked "the first factory", which is now *Gone* (not in the snapshot), so they pick Rubber by name;
- test data with `owner`: `arrangement-model.spec.ts`, `factory-draft.spec.ts`, `factory-page.spec.ts`, the recorded e2e answers (`web/e2e/fixtures/api.json`, 6 lines), and the MCP server's `IronWorld`. The contract check from step 99 (`FakeLedgerApi`) caught the last one: 28 MCP tests failed until it had `owner`.

**Test runs** (after the review fixes): .NET 1338 passed; web 491 passed (41 files); lint, Prettier and `ng build` clean; e2e on recorded answers 261 passed, 48 skipped (device-specific), as before. The journey against the real stack passed before the review fixes; they don't touch what it does.

## .NET / Angular points

| Here | In Java / Spring |
|---|---|
| **`factories.Sort(StoredFactory.OldestFirst)` after `ToListAsync`.** EF Core can't turn a C# `IComparer` into SQL, so the endpoint sorts in memory with the comparer that decides ownership, and the two can't drift. Primer: *Sorting by the domain's comparer after the query*. | Fetching with JPA and sorting with the domain's `Comparator`, rather than an `ORDER BY` that restates the rule. |
| **A positional record grows a parameter** (`MachineSummary(..., Guid Owner)`): every `new MachineSummary(...)` stops compiling, and `api/openapi.json` gets the field as required when the solution builds. | Adding a component to a Java `record`: every canonical constructor call breaks. |
| **`tsc --noEmit` skips templates.** `selectedOwners` changed type and only `ng build` saw that its `@for` needed an array. Primer: *`tsc` doesn't check templates*. | A JSP or Thymeleaf template the Java compiler never sees. |

## Review

Two axes, `/code-review` against `main`. No hard violations, nothing implemented wrongly. Fixed:

- **A guess was left** (both axes): `machineOwners` fell back to the first factory listing a machine when the owner wasn't among them, which Q9 rules out. It throws now. The back end guarantees the owner: it's the oldest factory whose match found the machine, so its trace lists it. The throw found test data from before `owner` existed, which a cast (`as MachineSummary`) or an untyped fake answer had let through: `arrangement-page.spec.ts` and `map-page.spec.ts` (20 tests).
- **`[0]` as owner in a template** (Standards): the defining panel's "already in {{ selectedOwners()[0].name }}" reads `selectedOwner().owner` now.
- **`[owner, ...alsoIn]` twice** (Standards): one `factoriesOf`.
- **The other factories weren't by name** (Spec, Q5): `alsoIn` is sorted by name.
- **The MCP server listed factories oldest first** (Spec, Q5): `list_factories` and its "No factory is called …" go through one `FactoriesByNameAsync`. Red first (Wire before Bolts), then green.
- **A stale comment** on `MapPage.owners`; `AreaPage.owners` got one.
- **Two behaviours in one test** (Standards): the unshared machine's owner is a test of its own; the web test says "as the trace says", not "the world".
- `FactoryTraces` looked a machine up twice.

The two web tests for the review fixes (others by name, the throw) were green at once: the code came first, as a review fix, not through the loop.

## Choices to overrule

- **The area's panel lists the owner first**, then the others by name, with the existing warning "a machine belongs to one factory at most". It doesn't say in words which one owns it.
- **A trace whose owner isn't listed throws** in `machineOwners`, so the page that reads it fails, rather than drawing a guessed owner.
- **The fixture was edited, not re-recorded:** in the recorded world no machine is shared, so each machine's owner is its own factory.

**Files:** `Factories/MachineSummary.cs`, `FactoryTraces.cs`, `FactoryEndpoints.cs`; `api/openapi.json`; `src/SatisfactoryLedger.Mcp/Tools/FactoryTools.cs`; `tests/SatisfactoryLedger.World.Tests/FactoryTracesTests.cs`, `tests/SatisfactoryLedger.Mcp.Tests/IronWorld.cs`, `FactoryToolsTests.cs`; `web/src/app/world/area/factory-outlines.ts` and spec, `area-page.ts`, `area-page.html`, `arrangement/arrangement-model.ts` and spec, `arrangement-page.ts` and spec, `map/map-page.ts` and spec, `factory-draft.spec.ts`, `factory-page.spec.ts`; `web/e2e/fixtures/api.json`; `docs/guide/primer.md`; `docs/architecture/reviews/2026-11-28.md`; this log.
