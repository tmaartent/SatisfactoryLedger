# Step 119 · Factories in a snapshot, and the factory pages read it

**Goal:** issue #57, the first ticket of card 4 (#56). One module, **factories in a snapshot** (`SnapshotFactories`), matches every stored factory in a snapshot once. The trace list, the factory page and the handovers' owners read it. No behaviour changes (decision 4). Branch `ticket/57-snapshot-factories`, from `refactor/factories-of-a-snapshot`.

## Baseline on the owner's world (before any change)

Measured by the orchestrator on `main` 74f0322 with curl against the running AppHost, world "Desert Start Cleaned" (72 factories), 2026-11-27. Each call includes loading the snapshot from Postgres. The "after" numbers come in step 120 (#58), when the arrangement reads the module too.

| Call | Warm-up | Warm runs (s) |
|---|---|---|
| Trace list `GET /api/v1/world/snapshots/{id}/factories` | 2.80 s | 2.09, 1.90, 1.91, 1.71, 1.84 |
| Factory page `GET …/factories/01a1ca2f-…/analysis` ("Iron Plates and Rods") | 2.54 s | 1.67, 1.61, 1.50, 1.69, 1.54 |
| Making an arrangement problem `GET /internal/world/arrangements/{run}/problem` (whole world, default settings) | — | 28.26, 23.05, 23.77 |

No speed-up is claimed in this step: nothing was measured after it.

## Design

- **Module:** `SnapshotFactories` (new, `World/Factories`, next to `MachineMatch`), the code home of **Factories in a snapshot**. It owns "turn a snapshot and the stored factories into traced factories", which was written out in `FactoryTraces.TraceAll`, `SnapshotAnalysis.ForFactory` and `WorldArrangement.Make`, and "who owns each machine", which `SnapshotHandovers.OwnersIn` worked out by matching every other factory again.
- **Interface (decision 7):**
  - `SnapshotFactories.Of(snapshot, factories)`: every match in one pass, the world graph built once.
  - `World` (the `WorldGraph`), `Factories` (in the caller's order), `Find(id)`, `Owners` (`IReadOnlyDictionary<BuildableId, Guid>`).
  - Per factory a `TracedFactory`: `Factory`, `Missing`, `IsTraceable`, `Members`. `Members` is traced the first time it's read (a `Lazy<FactoryMembers>`), and throws for a factory that isn't traceable.
  - `MachineMatch` stays as it is, the implementation the module calls, with its per-factory `taken` set (#60 changes that).
- **Callers moved onto it:**
  - `FactoryTraces.TraceAll`: reads `Factories`; no world graph or matching of its own.
  - `SnapshotAnalysis.ForFactory`: reads `Find(factory.Id)` and `World`. `Findings` now takes the world and the readings from its caller instead of building both a second time; the whole-world `For` builds them once and hands them over.
  - `SnapshotHandovers.Of(snapshot, factories, traced, readings)`: owners from `Owners`. `OwnersIn`, which matched every other factory again for each factory, is deleted.
  - `WorldArrangement.Make`: since it calls `SnapshotHandovers.Of`, it now builds `SnapshotFactories` once, takes its world graph from it, and its traced list reads `Factories` / `IsTraceable` / `Members`. Its `machinesOf` and `owned`, the arrangement-machines endpoint and `ClaimedMachines` are left for #58.
- **Owners keep today's rule** (decision 4): of the factories that have one machine, the first in the order given. The factory page still counts its own machines as its own: `SnapshotHandovers.OwnersFor` puts this factory's members first, then `Owners`. That's the same answer `OwnersIn` gave, without the matching.
- **The factory page with a factory not in the list:** the tests call `ForFactory(snapshot, factory, analysis, factories)` with lists that don't always hold `factory`. `ForFactory` adds it at the end then, so `Find` has it. The endpoint always passes the world's factories, the factory among them, so the order there is unchanged.
- **Deletion test:**
  - Delete `SnapshotFactories`: the trace list, the factory page and the arrangement each need the match, the definition, the traceable check and the members spelled out again, and the handovers need every factory matched per factory. It earns its keep.
  - `TracedFactory` is the per-factory answer; deleting it means tuples in three callers.
  - `OwnersIn` is deleted. `OwnersFor` is three lines that lay this factory's members over `Owners`.
- **Every new member has a production caller:** `Of`, `World`, `Factories`, `Find`, `Owners` (`SnapshotAnalysis`, `SnapshotHandovers`, `FactoryTraces`, `WorldArrangement`); `Factory`, `Missing`, `IsTraceable`, `Members` (`FactoryTraces`, `SnapshotAnalysis`, `SnapshotHandovers`, `WorldArrangement`).
- **Glossary:** new term **Factories in a snapshot**, code home `SnapshotFactories`. **Missing Machine** now names `TracedFactory.Missing` (matched in `MachineMatch`) and `FactoryTrace.Missing`.

### Choices to overrule

- **The own-machines exception lives in `SnapshotHandovers.OwnersFor`, not in the module.** Decision 7's interface has one `Owners` for the whole snapshot; "a factory counts its own machines as its own" depends on which factory asks. Kept at the one caller that needs it, so the interface stays as agreed. #59 could move it in (for example `TracedFactory.Owners`) when the rule changes to id order.
- **`Owners` holds every matched id, a missing machine's too** (under its own id, which the snapshot doesn't have), as `OwnersIn` did. Nothing looks such an id up; leaving them out would be cleaner but isn't a refactor.
- **`Members` throws `InvalidOperationException` for a factory that isn't traceable**, rather than returning empty members. Every caller checks `IsTraceable` first.
- **`Findings` takes the readings as well as the world**, since they were also built twice. Same result.
- **The arrangement's traced list already reads the module.** Leaving it on `MachineMatch` would have traced each factory twice (once there, once for the handovers' `Members`).

## Seams under test (decision 11)

- **A, the module:** `SnapshotFactoriesTests` (new), on the test world and hand-edited copies of it. Found by id; rebuilt within a metre; rebuilt further away, or as another class, is missing; not traceable; members (the logistics between its machines); owners (the first given; a rebuilt machine belongs to the factory it was matched to); found by its id. Four matching tests moved here from `FactoryTracesTests` (replace, don't layer). Only the first new test was red (a compile error, the module didn't exist); the others passed at once, because the behaviour already existed in `MachineMatch`. That's expected in a refactor: they pin the behaviour at the new interface.
- **B, the safety net:** the trace-list, factory-page (inputs, outputs, generators, findings), claimed-machines and arrangement tests and the golden fixtures pass without an edit.

Test run: 1327 passed, 0 failed.

## What #58 needs from the module

Nothing new on the interface, as far as I can see: the arrangement's `machinesOf` and `owned` can read `Factories` and `Members`; the arrangement-machines endpoint `Factories` and `Members` (or `Owners`); `ClaimedMachines.Among` `Owners` plus the factory names (`Factories[…].Factory.Name`, or `Find(owner)`). `MachineMatch.In` is then called only from `SnapshotFactories`.

## C# points

| Here | In Java / TypeScript |
|---|---|
| **`Lazy<T>` for a member traced on first read**: `TracedFactory.Members => members.Value`. The trace list and the arrangement read it for every traceable factory; the factory page only for its own. Primer: *Testing → `Lazy<T>` for a costly part of an object*. | Guava's `Suppliers.memoize`; TypeScript a caching getter. |
| **A `Lazy<T>` keeps the exception**: the factory function throws for a factory that isn't traceable, and every later read throws the same exception. Primer: *Testing → A `Lazy<T>` keeps the exception too*. | `Suppliers.memoize` retries instead. |
| **A `throw` expression** in a `?:` inside the lambda. Primer: *.NET basics → A `throw` expression*. | Java needs a block lambda with a `throw` statement. |
| **A collection expression in one branch of `?:`** takes its type from the other: `all.Any(…) ? all : [.. all, factory]`. Primer: *.NET basics → A collection expression in one branch of `?:`*. | Java spells out both sides; TypeScript just makes an array. |
| **A `private` constructor and a static factory method** (`SnapshotFactories.Of`): the only way to get one is the one pass that matches everything. | The same idiom as in Java (`List.of`, a private constructor). |

**Files:** `src/Modules/World/SatisfactoryLedger.World/Factories/SnapshotFactories.cs` (new), `Factories/TracedFactory.cs` (new), `Factories/FactoryTraces.cs`, `Analysis/SnapshotAnalysis.cs`, `Analysis/SnapshotHandovers.cs`, `Arrangement/WorldArrangement.cs`; `tests/SatisfactoryLedger.World.Tests/SnapshotFactoriesTests.cs` (new), `FactoryTracesTests.cs`; `docs/domain/glossary.md`, `docs/guide/primer.md`, this log.
