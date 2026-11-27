# Step 118 · The arrangement reads a factory's handovers

**Goal:** issue #54, the last ticket of card 3 (#50). The arrangement takes the sources feeding each factory, with the machines they start at, from `FactoryHandovers`, and no longer from the API record `InputSource`. The hidden `InputSource.Machines` is gone. No behaviour changes. Branch `ticket/54-arrangement-reads-handovers`, from `refactor/factory-handovers`.

## Design

- **Module:** `FactoryHandovers` (`World.Domain`), the code home of **Handovers**. The arrangement (`WorldArrangement`) is one more caller of its interface; the module itself doesn't change.
- **Interface, what changed:**
  - `FactoryHandovers`: nothing. The arrangement reads `Of(…).Inputs` (built through `SnapshotHandovers.Of`, as the factory page does) and `Ends(…)` for stations, which step 117 already moved in.
  - `FactoryInputs.Of(handovers, names)` and `FactoryOutputs.Of(handovers, names)`: the projections take the handovers and the factory names, and no longer build them. `SnapshotAnalysis.ForFactory` builds them once and hands them to both. Step 117 built them twice per `ForFactory`; that was the "if it's small, do it" of the ticket.
  - `InputSource` lost its last parameter `Machines` and the `[property: JsonIgnore]` on it. The API's JSON doesn't change, since the field was never written.
- **What the arrangement reads, field by field:**

  | Before (`InputSource`) | Now (`IncomingSource`) |
  |---|---|
  | `FactoryId` | `Owner` |
  | `Id` (a string) | `Id.Id` (a `BuildableId?`) |
  | `Machines ?? []` (null when none) | `Machines.Select(machine => machine.Id)` (empty when none) |
  | `PerMinute`, rounded by the projection | `SnapshotAnalysis.Round(PerMinute)`, rounded in the arrangement |
  | sources rounding to 0 left out by the projection | the same filter in the arrangement |

- **Making links no longer runs the "could bring more" maximum flow.** `FactoryInputs.Of` called `IncomingItem.Short()` for every input of every factory; the arrangement threw the answer away. Reading `Inputs` only follows the source lines; `Short()` runs only when the factory page reads it (decision 8).
- **Order of the ends at stations:** unchanged, through the static `FactoryHandovers.Ends`, in the order the factory's outputs come. Per-item ends from `Outputs` would put them in item-id order, which reaches the arrangement problem (see step 117).
- **Deletion test:**
  - Delete `InputSource.Machines`: nothing in production missed it but the arrangement, which now reads the domain. It was an API record carrying a field for a non-API caller.
  - Nothing new is added. The arrangement no longer depends on `FactoryInputs` or `InputSource`; those are now the factory page's projection only.
  - No member in `src/` is left without a production caller: `FactoryInputs.Of`, `FactoryOutputs.Of`, `SnapshotHandovers.Of` and `NamesOf` are called from `SnapshotAnalysis` (and `SnapshotHandovers.Of` from `WorldArrangement`); `IncomingItem.Short()` from `FactoryInputs`.
- **Glossary:** unchanged. **Source** and **Handovers** already name `FactoryHandovers` as their code home.
- **Docs:** ADR-0028's note on where a source's machines come from now names `IncomingSource.Machines`; the primer row on `[property: …]` says its example was deleted here.

### Choices to overrule

- **The arrangement rounds the sources as the factory page does** (`SnapshotAnalysis.Round`, dropping sources that round to 0). That keeps behaviour exactly as before (decision 2), but it means the arrangement uses the page's display precision. Unrounded rates would be more natural for the arrangement; it would be a (tiny) behaviour change in link rates, so I left it for its own issue if you want it.
- **One `ForFactory` test is deleted**, not kept unchanged: `FactoryInputsTests.A_source_of_machines_names_the_machines_it_starts_at` asserted on the hidden `InputSource.Machines`, which this ticket deletes. Seam A already covers the rule (`FactoryHandoversTests.Machines_of_another_factory_are_a_source_of_that_factory_with_the_machines_it_starts_at` and `Machines_of_no_factory_are_one_source_of_no_factory`), so this is "replace, don't layer". The other ForFactory tests, the arrangement tests and the golden fixtures pass without an edit.
- **The handovers are built once per `ForFactory`**, in `SnapshotAnalysis`, and passed to both projections.

## Seams under test (decision 12)

- **A, the module:** unchanged; its tests cover the sources with their machines.
- **B, the safety net:** the `ForFactory` tests less the one above, the golden fixtures, and the `WorldArrangementTests` (which cover the spot at the nearest machine and the station supply lines) pass without an edit.
- **C, line-following:** unchanged.

No new test: this is a pure refactor, and its proof is that the safety net stays green.

## Measured: making links

A temporary `Stopwatch` round the loop that reads each factory's inputs in `WorldArrangement.Make` (removed before committing), over the 22 calls `WorldArrangementTests` makes, three runs each:

| | First call (JIT-heavy) | The other 21 calls, together |
|---|---|---|
| Before (`FactoryInputs.Of`, with `Short()`) | 48.3 ms | 10.7 ms |
| After (`FactoryHandovers.Inputs`) | 41.1 ms | 8.5 ms |

About a fifth less on the arrangement tests' small worlds, close to the noise. No measurement on a real save was made, so no claim about a big world.

## C# points

| Here | In Java / TypeScript |
|---|---|
| **An extended property pattern through a nullable struct**: `source is { Kind: InputSourceKind.Building, Id.Id: { } building }`. `Id` is a `BuildableId?`; the pattern checks it isn't null and reads `.Id` of the value inside, in one go. Primer: *.NET basics → A positional pattern on a nullable tuple* is the same idea. | Java 21's record patterns nest (`instanceof Source(var kind, BuildableId(var id), …)`), but they don't unwrap an `Optional`. TypeScript: `source.id?.id`, then a check. |
| **Deconstructing a named tuple in `foreach`**: `foreach ((IncomingSource source, double perMinute) in input.Sources.Select(source => (Source: source, PerMinute: Round(…))))`. The names in the `Select` are for the `Where`; the `foreach` takes the parts by position. | No tuples in Java: a small record or `Map.Entry`. TypeScript: `for (const [source, perMinute] of …)`. |
| **A method's work moved out of a property path**: `Short()` being a method (step 116) is what lets the arrangement read `Inputs` without paying for the max-flow. | The same as not calling a costly getter in Java; here the compiler-visible `()` makes it obvious at the call. |
