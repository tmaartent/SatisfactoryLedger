# Step 129 · A snapshot's pipe networks, found in one place

From *Seen, not carded* in the architecture review of 2026-11-28 (`docs/architecture/reviews/2026-11-28.md`), made its own card the same day: spec #81. A refactor: no behaviour changes, no schema, API or analysis-version change.

## Design

- **Module:** the snapshot document, `World/Snapshots/SnapshotDocument.cs`, owns "which pipe networks are there" (BR-A6). Its interface widens by one method, `PipeNetworks()`, next to `PipeNeighbours()`: each network an `IReadOnlySet<string>` of piece ids, every pipe, junction, pump, valve and fluid buffer in exactly one. A piece that touches nothing is a network of its own; a snapshot from before pipe links (version 13) has every piece on its own.
- **Who asks it now:**
  - `PipeFlows.HubsOf` (the pipe flows) only decides which hub a network has: the hub next to the first of its pieces that a connection joins to one, as before.
  - `PipeHeadLifts.CarryingGas` (the head lifts) only decides which networks carry gas: those with a gas in any piece at save time. A network empty throughout still counts as carrying a liquid. It no longer needs the buildables by id.
- **What stays:** `HeadLift.Reaches` in the domain. It follows a pump one way only and visits a piece again when its reach grows, so it's a different walk, not a third copy of the flood fill. `PipeNeighbours()` stays public: the arrangement (`WorldArrangement`, `ExistingWays`) and `PipeNetworks()` itself use it.
- **Deletion test:** delete `PipeNetworks()` and the same flood fill comes back in `PipeFlows` and `PipeHeadLifts`, where a change to what joins a network (a new kind of piece, a valve that splits networks) would have to be made twice. It earns its keep.

## How it was built

1. **Seam (agreed):** `SnapshotDocument.PipeNetworks()`, in `SnapshotDocumentTests`, section *BR-A6: the pipe networks of a snapshot*. Small snapshots of pipes built by `OfPipes`, and `Networks` writes each network as its sorted ids so the assertion doesn't hang on set order.
   - `Pipes_that_dont_touch_make_separate_networks`: red (no method), then the flood fill over the pipe neighbours made it green.
   - `A_junction_joins_the_pipes_on_each_of_its_sides_into_one_network`: green at once; it pins the junction case the spec named.
   - `A_pipe_touching_nothing_is_a_network_of_its_own`: red (the flood fill only started from linked pieces), green once every pipe, junction and fluid buffer of the snapshot is a start too. A belt in the same snapshot is in no network.
2. **Callers moved:** `HubsOf` and `CarryingGas` rewritten on it, their own flood fills deleted.
3. **Guards, unchanged:** the pipe-flow tests, `HeadLiftTests`, the head-lift tests on the map, and the test worlds' stored analyses (not regenerated, and they didn't change).

**Test runs:** .NET 1372 passed (integration tests included, Docker running); the stored analyses unchanged.

## .NET points

| Here | In Java |
|---|---|
| **`IReadOnlyList<IReadOnlySet<string>>` as the return type**: a read-only view of the sets, which are `HashSet<string>` underneath. Callers can ask `Contains` and `Overlaps` but can't add. A public member stays on the interface; CA1859 only wants concrete types on private ones (primer, *CA1859*). | `List<Set<String>>` filled with `Set.copyOf(...)`, or `Collections.unmodifiableSet`. Java's read-only-ness is at run time (`UnsupportedOperationException`); C#'s `IReadOnlySet<T>` is a compile-time view, and a cast back to `HashSet<T>` would still write. |
| **`network.Overlaps(holdingGas)`** (primer, *`ISet<T>.Overlaps`*): whether two sets share any element. | `!Collections.disjoint(network, holdingGas)`. |
| **`foreach ((string edge, string hub) in hubNextTo)`** deconstructs each `KeyValuePair` (primer, *Deconstructing a dictionary in `foreach`*). | `for (var e : hubNextTo.entrySet())` with `e.getKey()`, `e.getValue()`. |
| **A `Dictionary` enumerates in insertion order, unpromised** (primer). The old `HubsOf` relied on it too: the first edge found of a network names its hub. | Only `LinkedHashMap` promises it; a `HashMap` would give a different order. |
| **`HashSet<string>` with `StringComparer.Ordinal`**: an id compares byte by byte, not by culture. | Java's `String.equals` is always ordinal; there's no culture to opt out of. |

## Choices to overrule

- **The return type:** a list of sets, as the spec said, rather than a lookup "network of piece". `HubsOf` builds that lookup itself from the list; `CarryingGas` doesn't need it.
- **Which buildables are pieces:** the logistics kinds `Pipe` (pipes, pumps, valves), `PipeJunction` and `FluidBuffer`, the same three `PipeHeadLifts.IsPiece` names, plus any id a pipe link names. The hub (`PipeNetwork`) is in none.
- **Order of the networks:** in the order of the buildables, then of the links; nothing relies on it.
- **A network touching two hubs** (not seen in the model) keeps the first hub found, as before. The save reader makes one hub per network, so it shouldn't happen.
- **The review's Status line** names the step and branch; the PR number goes in once it's opened.

**Files:** `src/Modules/World/SatisfactoryLedger.World/Snapshots/SnapshotDocument.cs`, `Analysis/PipeFlows.cs`, `Map/PipeHeadLifts.cs`; `tests/SatisfactoryLedger.World.Tests/SnapshotDocumentTests.cs`; `docs/guide/primer.md`; `docs/architecture/reviews/2026-11-28.md`; this log.
