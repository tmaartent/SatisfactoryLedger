# Step 120 · Arranging and defining read factories in a snapshot

**Goal:** issue #58, the second ticket of card 4 (#56). The arrangement, an arrangement's machines and the check when defining a factory (BR-W5) get their matched machines and owners from **factories in a snapshot** (`SnapshotFactories`), like the factory pages since step 119. No behaviour changes (decision 4). Branch `ticket/58-arranging-and-defining`, from `refactor/factories-of-a-snapshot`.

## Design

- **Module:** `SnapshotFactories` (`World/Factories`), the code home of **Factories in a snapshot**. After this step it's the only caller of `MachineMatch.In` in `src/` (the ticket's "done" check: `grep -rn "MachineMatch.In" src/` finds only `SnapshotFactories.Of` and the method itself).
- **Callers moved onto it:**
  - `WorldArrangement.Make`: nothing left to move. Step 119 already put its traced list on `Factories` / `IsTraceable` / `Members`, and `machinesOf` (the arranged factories' machines) and `owned` (what keeps the machines no factory owns apart) read that list's `Members.Machines`. Both are reads of the module; no matching remains in it. Making the problem matches each factory once (the 72 × 72 of `OwnersIn` went in step 119).
  - The arrangement-machines endpoint (`ArrangementEndpoints.MachinesAsync`): reads `SnapshotFactories.Of(document, factories).Factories` and each one's new `Machines`. Same list as before: per factory in the order the database gave, its stored machines' ids in the snapshot in their order, a machine two factories have listed under each (no owner rule, as today).
  - `ClaimedMachines.Among`: stays the one home of the BR-W5 check (decision 9), now a read of `Owners` and `Find(owner).Factory.Name`. Same signature, same result: first in the order given, in the order chosen.
- **Interface change: one member, `TracedFactory.Machines`** (`IReadOnlyList<BuildableId>`): per stored machine, in their order, its id in the snapshot (its own, or the one that replaced it; a missing one under its own id). The arrangement-machines endpoint needs it: `Members.Machines` is a set (no order), leaves out what isn't a machine in the world graph, and costs a trace of the factory's logistics, which a list of machine positions doesn't need.
- **Implementation change: the world graph and the traceable check are built when first read.** `SnapshotFactories.World` and `TracedFactory.IsTraceable` were computed in `Of`; now they're `Lazy<T>` over one shared `Lazy<WorldGraph>`. Matching still happens in one pass in `Of` (decision 7). Why: `ClaimedMachines` and the machines endpoint need only matches and owners. Building the world graph for them was work they never did before, and it throws on a snapshot whose connections name a buildable that isn't there. `ClaimedMachinesTests.A_machine_rebuilt_within_a_metre_is_still_taken_by_the_factory_of_the_one_it_replaced` swaps a smelter's id without its connections, and failed with an eager world. It now passes unchanged.
- **Deletion test:**
  - `TracedFactory.Machines`: delete it and the machines endpoint needs `MachineMatch.In` again (a second copy of the match) or `Members.Machines` (a trace per factory, and a different order). It earns its keep.
  - The `Lazy` world: delete it and the BR-W5 check and the machines endpoint build a world graph they never read.
  - `ClaimedMachines` stays as a module of its own: it's the one place that turns owners into "taken by factory X" for BR-W5, and `DefineFactoryHandler` calls it.
- **Every new member has a production caller:** `TracedFactory.Machines` (`ArrangementEndpoints.MachinesAsync`). `World` and `IsTraceable` keep their callers from step 119.
- **Glossary:** **Factories in a snapshot** now lists all its readers. No new term.

### Choices to overrule

- **`TracedFactory.Machines` is a new interface member.** The alternative, `Members.Machines`, would trace every arranged factory's logistics to list machine positions, and changes the order inside a factory (a set). Kept to the matched ids, which is what the endpoint read from `MachineMatch` before.
- **The world graph is lazy**, rather than editing `ClaimedMachinesTests` to keep the connections consistent. A real snapshot's connections always name buildables it has, so for the owner this changes only the cost: the BR-W5 check and the machines endpoint no longer build a graph.
- **`owned` in `WorldArrangement` stays on `Members.Machines`, not `Owners`.** `Owners` also holds a missing machine's own id and the machines of a traceable factory without an outline; `owned` doesn't. Same answer for every machine in the snapshot that it's checked against, but not the same set, so left as it is in a refactor.
- **The machines endpoint keeps the database order of the factories** (no `OrderBy`), as before (decision 10: each caller's order stays).

## Seams under test (decision 11)

- **A, the module:** `SnapshotFactoriesTests` gains one test, `A_factorys_machines_in_the_snapshot_are_its_stored_machines_a_rebuilt_one_under_the_id_that_replaced_it`. Red first (compile error: no `Machines`), then green.
- **B, the safety net:** `ClaimedMachinesTests`, `WorldArrangementTests`, the factory tests, `WorldArrangementApiTests` (which calls the machines endpoint) and the golden fixtures pass without an edit.

Test run: 1328 passed, 0 failed.

## Timings on the owner's world

Baseline measured by the orchestrator on `main` 74f0322 with curl against the running AppHost, world "Desert Start Cleaned" (72 factories), 2026-11-27. Each call includes loading the snapshot from Postgres.

| Call | Warm-up | Warm runs (s) |
|---|---|---|
| Trace list `GET /api/v1/world/snapshots/{id}/factories` | 2.80 s | 2.09, 1.90, 1.91, 1.71, 1.84 |
| Factory page `GET …/factories/01a1ca2f-…/analysis` ("Iron Plates and Rods") | 2.54 s | 1.67, 1.61, 1.50, 1.69, 1.54 |
| Making an arrangement problem `GET /internal/world/arrangements/{run}/problem` (whole world, default settings) | — | 28.26, 23.05, 23.77 |

**After:** measured by the orchestrator once this step is merged into `refactor/factories-of-a-snapshot`. No speed-up is claimed until then.

## C# points

| Here | In Java / TypeScript |
|---|---|
| **One `Lazy<T>` shared by many objects**: `Lazy<WorldGraph> world = new(snapshot.ToWorldGraph);` is handed to every `TracedFactory`, whose own `Lazy<bool>` and `Lazy<FactoryMembers>` read `world.Value`. The graph is built once, by whichever read comes first, or never. Primer: *Testing → One `Lazy<T>` shared by others*. | One `Suppliers.memoize(snapshot::toWorldGraph)` passed to each object. |
| **A method group as the `Func<T>`**: `new(snapshot.ToWorldGraph)`, no lambda. Primer: *.NET basics → Method groups as delegates*. | `snapshot::toWorldGraph`. |
| **An expression-bodied property over a `Lazy`**: `public bool IsTraceable => isTraceable.Value;` replaced `{ get; }` without any caller noticing: a property is an interface, a field isn't. | A Java getter hides the same change; a public field wouldn't. |

**Files:** `src/Modules/World/SatisfactoryLedger.World/Factories/SnapshotFactories.cs`, `Factories/TracedFactory.cs`, `Factories/ClaimedMachines.cs`, `Arrangement/ArrangementEndpoints.cs`; `tests/SatisfactoryLedger.World.Tests/SnapshotFactoriesTests.cs`; `docs/domain/glossary.md`, `docs/guide/primer.md`, this log.
