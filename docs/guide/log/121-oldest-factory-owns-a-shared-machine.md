# Step 121 · The oldest factory owns a machine two factories have

**Goal:** issue #59, the third ticket of card 4 (#56), decision 2. A **change in behaviour**, not a refactor: a machine that two factories have (stored before BR-W5) has one owner in the whole snapshot, the **oldest** factory (lowest id; ids are `Guid` v7), whatever order the caller passes. A factory still counts its own machines as its own. Branch `ticket/59-oldest-factory-owns`, from `refactor/factories-of-a-snapshot`.

## Design

- **Module:** `SnapshotFactories` (`World/Factories`), the code home of **Factories in a snapshot**. The owner rule lives there and nowhere else.
- **Implementation change:** `Of` kept the first factory per machine (`owners.TryAdd`); it now keeps the one with the lower id (`factory.Id.CompareTo(owner) < 0`). `Factories` keeps the caller's order (decision 10): the trace list by name, the arrangement by id, the define check in database order.
- **Interface change: one member, `OwnersSeenFrom(TracedFactory)`**, which moves the exception "a factory counts its own machines as its own" into the module. It was `SnapshotHandovers.OwnersFor`, a private helper next to the caller (step 119's choice to overrule suggested moving it here). It's the same five lines; the helper is deleted, and `SnapshotHandovers.Of` calls `factories.OwnersSeenFrom(factory)`. Now both halves of the BR-W5 owner rule (oldest owns; own machines are its own) are in one place, and the glossary term keeps one code home.
- **Deletion test:** delete `OwnersSeenFrom` and the exception goes back to a helper in `Analysis`, next to the module whose `Owners` it overrides: a second place that decides ownership. It earns its keep. Its one production caller is `SnapshotHandovers.Of` (the factory page and the arrangement's inputs).
- **Not moved:** `owned` in `WorldArrangement` still reads `Members.Machines` (step 120's choice). It asks "does any factory own this machine", which the owner rule doesn't change.

## The change in behaviour, and where it shows

`Owners` is read by the handovers (who a source or destination belongs to, on the factory page and in the arrangement's inputs) and by `ClaimedMachines` (the 409 `World.MachinesTaken` when defining or editing a factory). Before, of two factories with the same machine, the first in the caller's order won.

- **Seen from a third factory**: a factory whose input comes from an overlapped machine names that machine's owner. The factory page and the arrangement already listed factories by id (`OrderBy(other => other.Id)` in Postgres, which sorts a `uuid` the way `Guid.CompareTo` does), so for them the answer is the same as before; it now no longer depends on that `OrderBy`.
- **Where it changes**: the define check passes the world's other factories in database order (no `OrderBy`). Choosing a machine two older factories both have now names the oldest of them in the 409, where it named whichever Postgres returned first.
- **From inside a factory**: its own machines stay its own (`OwnersSeenFrom`), so its findings don't change. The golden fixtures pass unchanged.

## Seams under test (decision 11)

- **A, the module** (`SnapshotFactoriesTests`):
  - Red first: `A_machine_two_factories_have_belongs_to_the_oldest_of_them_whatever_the_order_given`. Two factories with fixed v7 ids, passed newer first. It failed with "Expected …0001, Actual …0002", then passed.
  - Red first (compile error: no `OwnersSeenFrom`): `A_factory_counts_a_machine_it_shares_with_an_older_one_as_its_own` and `Seen_from_a_factory_the_machines_of_others_belong_to_their_owners`.
  - `The_factories_keep_the_order_given` (decision 10). It passed at once: it guards that the new rule didn't reorder `Factories`, it doesn't drive code.
- **B, the safety net:** `ClaimedMachinesTests`, `WorldArrangementTests`, the factory-page tests and the golden fixtures pass without an edit.

**Tests changed because of the new rule:** one. `A_machine_two_factories_have_belongs_to_the_first_of_them_given` stated the old rule (first in the order given), so it's replaced by the red test above (replace, don't layer). No other test was edited.

Test run: 1331 passed, 0 failed.

## C# points

| Here | In Java / TypeScript |
|---|---|
| **`Guid.CompareTo` orders like the database**: by the written-out string, first group first, unsigned. So "lowest id" is "created first" for v7 ids, in C# and in Postgres's `ORDER BY id`. Primer: *Comparing `Guid`s: `CompareTo`*. | `UUID.compareTo`; check its order against the database's before relying on it. |
| **`TryGetValue` with an `out` variable**: `if (!owners.TryGetValue(id, out Guid owner) \|\| factory.Id.CompareTo(owner) < 0)` looks up and binds in one call; `owner` is in scope after the condition. | `Map.get` and a null check, or `map.merge(id, factory.id(), (a, b) -> a.compareTo(b) <= 0 ? a : b)`. |
| **Fixed `Guid`s in tests**: `Guid.Parse("019ac000-…-0001")`. Two `Guid.CreateVersion7()` made in the same millisecond have random bits after the timestamp, so the test can't trust creation order. | `UUID.fromString(...)`. |

## Choices to overrule

- **`OwnersSeenFrom` is a method on `SnapshotFactories`**, not a property on `TracedFactory`. It needs the whole snapshot's `Owners` and one factory's members; on `SnapshotFactories` it reads as "owners, seen from this factory". It builds a new dictionary on each call (as `OwnersFor` did), so it's a method, not a property.
- **It keeps `Members.Machines`** for the factory's own machines, as `OwnersFor` did, not `TracedFactory.Machines`. So it's only for a traceable factory, which is the only kind the handovers see. Switching would also count a missing machine's id, which the snapshot doesn't have: harmless, but a change nobody asked for.
- **Ties**: two factories with equal ids can't happen (primary key). Two defined in the same millisecond would be ordered by v7's random bits rather than by which came first; I left that alone, since a player can't define two factories in one millisecond.

**Files:** `src/Modules/World/SatisfactoryLedger.World/Factories/SnapshotFactories.cs`, `Analysis/SnapshotHandovers.cs`; `tests/SatisfactoryLedger.World.Tests/SnapshotFactoriesTests.cs`; `docs/domain/business-rules.md` (BR-W5), `docs/domain/glossary.md` (Factory, Factories in a snapshot), `docs/guide/primer.md`, this log.
