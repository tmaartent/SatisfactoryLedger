# Step 128 · Classify buildables once, in the save-file adapter; the fluid truck station in the arrangement

Card D of the architecture review of 2026-11-28 (`docs/architecture/reviews/2026-11-28.md`), spec #78, grilled 2026-11-28. A fix first, then a refactor that changes no behaviour, then the glossary.

## Design

- **Domain rule (owner, 2026-11-28):** a fluid truck station is a station in the arrangement. It keeps its plots whether or not it feeds a factory, and a pipe may go down to it. Now in BR-O1. The kinds and their names are the owner's too; "end consumer" was chosen over "uses up".
- **Module:** the save-file adapter, `World.SaveFile`, owns "what kind of buildable is this". It publishes one answer, `GameClasses.KindOf(className)`, returning a `BuildableKind`: `Sink`, `EndConsumer`, `Uploader`, `Station`, `Generator`, `BeltOrLift`, `Pipe`, or `Other`. Its interface widens by that one method and the enum. `IsGenerator` and `IsUploader` are gone from it. `RoleOf` and `StockInventoryOf` now ask the table instead of keeping their own sets.
- **Derived on read, not stored.** `BuildableDocument.BuildableKind` and `TransportTierDocument.BuildableKind` are computed properties with `[JsonIgnore]`, so the stored JSON stays as it was. No snapshot version bump (ADR-0016), and every snapshot already imported gets the fix.
- **Who asks it now:**
  - the arrangement's stations and spots (`WorldArrangement`, which had four station classes of its own) and its top belt and pipe capacities (it had `Contains("Pipeline")`);
  - the handovers' sinks and end consumers (`SnapshotHandovers`, which had `SinkClass` and `UsesUp`);
  - the save-file reader's generator and uploader checks.
- **A generator, defined once:** `BuildableDocument.IsGenerator(copied)`: its kind is `Generator`, and the Catalog copied at import knows its burn rate (BR-A8). `SnapshotDocument.IsMachineOrGenerator` (BR-W2) and `ToBuildable`'s `Consumer.IsGenerator` both ask it. They had been a list search and a dictionary key check.
- **What stays:** a class name used as a *key* into a table copied from the Catalog (footprints, transport tiers, extractors, generators' rates, storages) or shown (findings, traces). The handovers still get the id → class name map, for display (card E may change that).
- **Deletion test:** delete `KindOf` and the class-name sets come back in four files, as they were, and they can drift apart again. That drift is what left the fluid truck station out. The table earns its keep.

## How it was built

1. **Fix, red first:** a small world in `WorldArrangementTests`, with a packager unpacking turbofuel into a pipe to a fluid truck station 50 metres away. `A_fluid_truck_station_in_the_area_blocks_its_plots` found no fixed place at all. `A_fluid_truck_station_a_factory_sends_fluid_to_is_a_spot_its_pipe_goes_down_to` found no spot. One more class name in `WorldArrangement.Stations` made both green. That's the `fix(world):` commit, with the old shape of the code.
2. **Refactor:** `GameClassesTests.Each_class_has_its_kind` is the agreed seam, one row per class and its kind, plus geothermal, a pipe support and a few others that are `Other`. It was red until `KindOf` existed. Then every classifying use was moved onto the kind, and `WorldArrangement.Stations` was deleted. `A_dimensional_depot_uploader_takes_only_what_is_left_over` is replaced by the table's `Uploader` row. The fluid truck station also got a row in `Platforms_drone_ports_and_truck_stations_are_stations`.
3. **Guards, unchanged:** the snapshot-document, handovers, arrangement, snapshot-analysis and simulation suites. The test worlds' stored analyses were not regenerated, and they didn't change.

**Test runs:** .NET 1369 passed (integration tests included, Docker running); the stored analyses unchanged.

## .NET points

| Here | In Java |
|---|---|
| **`[JsonIgnore]` on a computed property** (`BuildableDocument.BuildableKind`): System.Text.Json writes every public getter, so without it every stored buildable would gain a field. | Jackson does the same with a `getX()`, and `@JsonIgnore` on it is the same fix. |
| **`=>` rather than `{ get; } = …`** for a value derived from another property of a record. `with { ClassName = … }` copies fields and doesn't run initialisers, so a stored kind would go stale on the copy. | Java records have no `with`; a wither calls the canonical constructor, which would recompute it. |
| **`is { BuildableKind: BuildableKind.Station }`** on `GetValueOrDefault(id)`: the property pattern is the null check too. | `Optional.ofNullable(map.get(id)).map(…).filter(…).isPresent()`, or two conditions. |
| **A property named after its type** (`BuildableKind BuildableKind`): the compiler reads a type where it expects one and a member where it expects one. | Java keeps types and members in separate namespaces anyway; a getter `getBuildableKind()` never clashes. |
| **`internal` enum in a `[Theory]`**: a public test method can't take it (CS0051), so the rows pass its name as a string and `Enum.Parse` it. | JUnit 5's `@EnumSource` / `@CsvSource` take a package-private enum without complaint. |

## Choices to overrule

- **A generator is the kind *and* the Catalog copy knowing it.** The kind alone would make a generator in a snapshot older than version 7 (whose Catalog copy has no generators) one of a factory's possible machines. That's a behaviour change, so it was left out of the refactor. The glossary's definition ("burns fuel, and the Catalog knows its burn rate") says both.
- **One enum for both questions:** what a buildable is, and what a transport tier carries (`BeltOrLift`, `Pipe`). Pumps and valves are `Pipe` too, since their logistics kind is. Only the tiers ask that.
- **`Other`** is the name for every class none of the kinds covers.
- **The kind is a property on the two documents** (`BuildableDocument`, `TransportTierDocument`), not a lookup kept on `SnapshotDocument`. It's a dictionary lookup each time it's asked, and it can't go stale.
- **BR-O1** now lists the stations by name, the fluid truck station among them.
- **The review's Status line for card D** names the step and branch; the PR number goes in once it's opened.

**Files:** `World.SaveFile/BuildableKind.cs` (new), `GameClasses.cs`, `SaveFileReader.cs`; `World/Snapshots/BuildableDocument.cs`, `SnapshotDocument.cs`, `TransportTierDocument.cs`; `World/Arrangement/WorldArrangement.cs`; `World/Analysis/SnapshotHandovers.cs`; `tests/SatisfactoryLedger.World.Tests/WorldArrangementTests.cs`, `tests/SatisfactoryLedger.World.SaveFile.Tests/GameClassesTests.cs`; `GLOSSARY.md`; `docs/domain/business-rules.md`; `docs/guide/primer.md`; `docs/architecture/reviews/2026-11-28.md`; this log.

## Review fixes

From the code review of PR #82.

1. **The web's station lists left out the fluid truck station.** `arrangement-model.ts` and `arrangement-page.ts` each kept a `stationClasses` set, and neither held `Build_FluidTruckStation_C`: a line from one was marked as an undefined factory, and the page drew it as a plain building. The model's set is now the only one, exported, and the page imports it. Red first: `marks a fluid truck station with the map's dark dot, as a station` in `arrangement-model.spec.ts`. No API change: the web still decides it from the class name in a spot's id, as before. A `fix(web):` commit.
2. **`SaveFileReader` asks `GameClasses.KindOf` once per buildable**, into a local `kind`, instead of three times in the consumer's initialiser.
3. **`BuildableDocument.IsGenerator(burnRateCopied)`** takes a `Func<string, bool>`: whether the Catalog copied at import has a generator of that class. `ToBuildable` passes `catalog.Generators.ContainsKey`, a method group, so it stays a key lookup in the dictionary it already had; `SnapshotDocument.IsMachineOrGenerator` passes a lambda over its list, as it searched before. The old `IEnumerable<GeneratorDocument> copied` made `ToBuildable` hand over `.Values` and search them in a line. Behaviour unchanged.
4. **`GameClasses.RoleOf` is left as it is.** It names the four consumer kinds once, and that's a different fact from the `Kinds` table: the table says what a class is, `RoleOf` says which kinds the reader turns into a consumer. Putting the role into the table would repeat it on every row; a set of consumer kinds, or a method on the enum, would only move the same four names elsewhere.
5. Card D's **Status:** line names the branch, `fix/classify-buildables-once`, and PR #82.

**Points worth noticing:**

| Here | In Java / TypeScript |
|---|---|
| **A method group as the predicate** (`IsGenerator(catalog.Generators.ContainsKey)`): the dictionary's own `ContainsKey` is the `Func<string, bool>`, no lambda (see the primer's *Method groups as delegates*). Taking a predicate rather than a collection lets each caller keep its own lookup. | Java's `IsGenerator(catalog.generators()::containsKey)` into a `Predicate<String>`. |
| **`export const stationClasses: ReadonlySet<string>`**: the page imports the model's set, typed read-only, so neither file can `add` to the other's list. | TypeScript only; a Java `Set.of(...)` is immutable at run time, `ReadonlySet` only at compile time. |

## The owner's decision on generators

Decided 2026-11-28, after the review: snapshots from before version 7 don't need to be supported, so **a generator is its kind alone**. Choice 1 under *Choices* and review fix 3 are undone:

- `BuildableDocument.IsGenerator` is a property again, `BuildableKind == Generator`, with no predicate. `Consumer.IsGenerator` is set from it.
- `IsMachineOrGenerator` (BR-W2) moved from `SnapshotDocument` to `BuildableDocument`: it no longer needs the snapshot's Catalog copy, only the buildable. The five callers ask the buildable; the map and the analysis summary, which also checked the role, now just ask `IsGenerator`.
- The Catalog copy still gives a generator its **rates** (`GenerationOf`); a generator whose fuel it doesn't know still has none (BR-A15).

| .NET / C# | Java / Spring |
|---|---|
| **Moving a method to the data it reads** (`snapshot.IsMachineOrGenerator(buildable)` → `buildable.IsMachineOrGenerator`): a method group like `.Where(snapshot.IsMachineOrGenerator)` becomes a lambda, `.Where(buildable => buildable.IsMachineOrGenerator)`, because a property isn't a method group. | `filter(snapshot::isMachineOrGenerator)` → `filter(BuildableDocument::isMachineOrGenerator)`. Java's unbound method reference keeps it point-free; C# has no unbound form for a property. |