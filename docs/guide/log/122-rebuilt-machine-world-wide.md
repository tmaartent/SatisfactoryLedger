# Step 122 · A rebuilt machine replaces one stored machine across the world

**Goal:** issue #60, the fourth ticket of card 4 (#56), decision 3. A **change in behaviour**, not a refactor: a rebuilt machine (a new id, the same class, within a metre of where a stored machine stood) replaces **at most one stored machine across all the world's factories** in a snapshot. When stored machines of two factories both match it, the **oldest** factory (lowest id, as in BR-W5) gets it, and the other reports its machine as **missing**. Before, each factory was matched on its own, with its own "already taken" set, so both could claim it. Branch `ticket/60-rebuilt-machine-world-wide`, from `refactor/factories-of-a-snapshot`.

## Design

- **Module:** `SnapshotFactories` (`World/Factories`), the code home of **Factories in a snapshot**; the interface under test. Its interface doesn't change: `Of(snapshot, factories)` already took every factory at once (step 119), so the rule could move inside without a caller noticing.
- **Implementation change, in `MachineMatch`** (internal, one caller: `SnapshotFactories.Of`). `MachineMatch.In(snapshot, machines)` matched one factory; it's now `MachineMatch.In(snapshot, factories)`, which matches all of them in one pass and returns one `MachineMatch` per factory, in the order given. Inside it:
  - **taken** starts as every stored machine of *any* factory that the snapshot has by its id, so such a machine is never another factory's replacement;
  - replacements are chosen **oldest factory first** (`OrderBy(factory => factory.Id)`, a stable sort), each stored machine taking the closest free machine of its class within a metre, as before;
  - the result per factory is read back in the caller's order (decision 10: `Factories` keeps it).
- **Wiring change at one caller, the arrangement-machines endpoint** (`ArrangementEndpoints.MachinesAsync`). It passed only the arranged factories to `SnapshotFactories.Of`; it now passes the world's factories and keeps the arranged ones. Otherwise a rebuilt machine that an older factory *outside* the arrangement gets would be drawn as an arranged factory's machine, unlike on every other page. The arrangement problem itself (`ArrangementProblems.FactoriesAsync`), the trace list and the factory page already pass the whole world.
- **Deletion test:** nothing new to delete. `MachineMatch` stays the one place that knows a rebuilt machine (class, a metre, taken); it's deeper now, since "across the world" is inside it instead of being nobody's.
- **Second copy:** none. `MachineMatch.In` has one caller; the factory preview doesn't match stored machines.

## The change in behaviour, and where it shows

Only when two factories' stored machines are within a metre of one rebuilt machine of their class, or when a factory's stored machine is gone and another factory's machine (kept by its id) stands within a metre of where it stood.

- **Trace list, factory page, arrangement:** the younger factory now reports its machine as missing there, and the rebuilt machine is the oldest one's in `Owners`. Before, both traced it, so both counted it as a member.
- **Arrangement machines:** see the wiring change above.
- **The define check** (`ClaimedMachines`, 409 `World.MachinesTaken`) passes the world's *other* factories, so the factory being edited isn't part of the match. See *Choices to overrule*.

The golden fixtures (stored analyses) passed unchanged: their test worlds have no two factories near one rebuilt machine, so no regeneration was needed.

## Seams under test (decision 11)

- **A, the module** (`SnapshotFactoriesTests`):
  - Red first: `A_rebuilt_machine_two_factories_matched_is_the_oldest_ones_and_missing_from_the_other`. SmelterA rebuilt in place; the older factory stores SmelterA, the younger one a smelter 30 cm away; passed younger first. It failed with "Expected [StoredMachine … Build_SmelterMk1_C_9 …], Actual []" (the younger one claimed it too), then passed.
  - `A_machine_another_factory_has_by_its_id_replaces_no_older_factorys_machine` (the ticket's second criterion). It passed at once: slice one's world-wide `taken` set already covers it. Against the old per-factory matcher it would fail (the older factory's `taken` didn't hold the younger one's SmelterB), so it guards the rule rather than driving code.
- **B, the safety net:** `ClaimedMachinesTests`, `WorldArrangementTests`, `WorldArrangementApiTests` (the machines endpoint), the factory-page tests and the golden fixtures pass without an edit.

**Tests changed because of the new rule:** none. No existing test had two factories near one rebuilt machine.

Test run: 1333 passed, 0 failed (two more than step 121).

## C# points

| Here | In Java / TypeScript |
|---|---|
| **`Zip` into tuples**: `stored.Zip(matches)` pairs the factories with their matches; `foreach ((StoredFactory factory, MachineMatch match) in …)` deconstructs each pair. Primer: *`Zip` into tuples, and `OrderBy` is stable*. | No `zip` on Java streams (Guava `Streams.zip`, or an index loop); lodash `zip` in TypeScript. |
| **`OrderBy` is stable**: factories with equal ids (impossible, primary key) would keep the order given. Same primer entry. | `Stream.sorted` on an ordered stream is stable too. |
| **A collection expression from a `Select`**: `return [.. factories.Select(factory => Matched(factory.Machines))];` into an `IReadOnlyList<MachineMatch>`; the compiler picks the concrete type. | `.toList()`. |

## Choices to overrule

- **A stored machine two factories have (from before BR-W5) gets one replacement, which both keep.** It's one machine with one id, so "replaces at most one stored machine" holds; the oldest factory owns the replacement (BR-W5), and the younger one still counts it as its own from inside (`OwnersSeenFrom`). The alternative, the younger one reporting its copy as missing, would make an overlap look different once the machine is rebuilt than before. I chose the first; say if you want the other.
- **The define check doesn't include the factory being edited.** When you edit an older factory X whose stored machine was rebuilt, and a younger factory Y's stored machine also matches the rebuilt one, the check matches without X, so Y gets the rebuilt machine there and choosing it for X is refused as Y's. Everywhere else it's X's. Fixing it means passing X's stored version too and leaving X out of the claims; I left `ClaimedMachines` as it is, since the case needs two factories' machines within a metre of each other, which BR-W5 has refused since 2026-11-11.
- **Greedy, per stored machine.** Within the oldest factory each stored machine takes the closest free one in its order, as before. A younger factory's closer stored machine still loses to an older factory's farther one: that's the rule as decided ("the oldest factory gets it").

**Files:** `src/Modules/World/SatisfactoryLedger.World/Factories/MachineMatch.cs`, `Factories/SnapshotFactories.cs`, `Arrangement/ArrangementEndpoints.cs`; `tests/SatisfactoryLedger.World.Tests/SnapshotFactoriesTests.cs`; `docs/domain/business-rules.md` (BR-W11), `docs/domain/glossary.md` (Missing Machine, Factories in a snapshot), `docs/guide/primer.md`, this log.
