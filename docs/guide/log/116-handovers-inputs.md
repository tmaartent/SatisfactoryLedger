# Step 116 · A factory's handovers, the inputs side

**Goal:** issue #52, the second ticket of card 3 (#50). One domain module, `FactoryHandovers` in `World.Domain`, answers per item what a factory takes in: what arrives, what it needs from outside at 100%, how short it is, and where what arrives comes from. The rules that lived in the API layer's lambdas now live in it. No behaviour changes. Branch `ticket/52-handovers-inputs`, from `refactor/factory-handovers`.

## Design

- **Module:** `FactoryHandovers` (`World.Domain`), next to `FactoryMembers`. It owns the glossary term **Handovers**. This step builds the inputs side only.
- **Interface:**
  - `FactoryHandovers.Of(world, readings, members, owners, sinks, buildingOf)` returns the handovers. `Inputs` is a list of `IncomingItem`, ordered by item id. Each has `Item`, `Arriving`, `Need`, `Sources` and the method `Short()`.
  - An `IncomingSource` has `Kind`, `Owner` (a `Guid`, decision 7), `Building`, `Id`, `PerMinute` and `Machines`.
  - Values are unrounded (decision 9). `Short()` runs the maximum flow only when it's called (decision 8).
  - `FactoryHandovers.Arriving(world, readings, members, input, item)` tells what arrives over one input. The factory preview calls it, and so does `Of`.
- **The rules moved in:**
  - a station could bring more as far as its stock, with that of the containers its line unloads into, lasts 30 minutes (BR-A17);
  - a factory's own machines are no outside supply;
  - what overflows into a sink could come instead;
  - a shortfall within 0.01% of the need is none;
  - the kind of each source.
- **Facts from the caller (decision 3):**
  - `owners`: who each machine belongs to, as a `Guid`;
  - `sinks`: the AWESOME Sinks, as a set of ids;
  - `buildingOf`: each building's class.
  - Every rule on those facts is inside the module. `usedUp` (the consumers that use up what they get) is left for #53: only the outputs side reads it, so taking it now would add a parameter with no use.
- **What became private:** `InputSources.CouldBringMore` and its `MaximumFlow` are now private to `FactoryHandovers` (decision 11). Its four callbacks became two private local functions (`Spare` and `Sunk`), and it reads room and what's carried from `FlowReadings` itself. `InputSources` keeps only the line-following `Of`, which the traces call too.
- **Moved:** `InputSourceKind` moved from `World.Analysis` to `World.Domain`. It was `internal`, now `public`. The API record `InputSource` reuses it, and its JSON names don't change.
- **Projection:** `FactoryInputs.Of` builds the facts from the snapshot and calls the module. It rounds the values, drops sources that round to nothing, and looks up the supplying factory's name by its `Guid`. The arrangement still reads `FactoryInputs.Of` and the hidden `InputSource.Machines`; #54 changes that.
- **Deletion test:**
  - Delete `FactoryHandovers`, and the rules go back into the API layer's lambdas, with the preview's `Arriving` beside them.
  - `IncomingItem` and `IncomingSource` exist so the projection doesn't reach into tuples.
  - Nothing new is test-only. Each member of the module has a production caller in `FactoryInputs` or `FactoryTraces`.
- **Glossary:** a new row **Handovers** → `FactoryHandovers`. The **Source** (of an input) row points at it.

### Choices to overrule

- **The preview reads `Arriving` per input**, a second static entry point on the module, and doesn't call `Of`. The preview has no saved factories, so it couldn't give owners. Calling `Of` would also follow every source line, and the preview never reads the sources.
- **`buildingOf` is a dictionary** (`IReadOnlyDictionary<BuildableId, string>`), not a `Func`: it's a fact, as decision 3 has it. The projection builds it from all of the snapshot's buildables, once per factory. The old code built the same string-keyed dictionary.
- **`sinks` is a set of ids**, not a class name. Which class is a sink (`FactoryOutputs.SinkClass`) is still known only to the API layer. Classifying at import is card 8.
- **`Short()` keeps its name and suppresses CA1720** ("identifier contains type name"). It's the glossary's word. This is the first `[SuppressMessage]` in `src/`. The other option would have been `Shortfall()`.
- **`IncomingSource.Machines` is empty, not null,** for kinds other than a factory or no factory. The projection turns empty back into `null`, so the hidden field the arrangement reads stays the same.

## Seams under test (decision 12)

- **A, new:** `FactoryHandoversTests`, which tests the module through `Of` on a `TestWorld` with `TestReadings`. There are 18 tests:
  - what arrives and the need;
  - five on sources: another factory with its machines, no factory, most first, a building by its class, nowhere;
  - twelve on short: the eight old `CouldBringMore` cases rewritten, two for stations (BR-A17), one for its own machines, one for the tolerance.
  - It catches the rules. It misses how the factory page rounds and names them.
- **B, the safety net, unchanged:** the `ForFactory` tests (`FactoryInputsTests`, `FactoryOutputsTests`) and the golden fixtures. They pass without an edit.
- **Replace, don't layer:** the eight `CouldBringMore` lambda tests and their `PerMinute` helper are deleted from `InputSourcesTests`. Its four line-following tests stay (seam C).

### How the lambda tests became readings

| Before | Now |
|---|---|
| `room: ("ore", 200)` by hand | `Belt("ore", 120)` and `.Into("ore", …, 60)`: `FlowReadings.Room` is capacity less what's carried |
| `("split", +∞)` by hand | Nothing: a splitter has no capacity, so its room is +∞ |
| `spare: ("miner", 60)` | `.Spare("miner", …)` for a provider; `.Starved(…, 0.5)` for a machine of another factory, which makes the rest more |
| `sunk: ("sink", 30)` | `sinks: ["end"]` and `.Into("overflow", …, 30)`: what reaches the sink |
| Starts passed as `[Id("ore")]` | Starts found by the module from the factory's inputs |

The start of the walk is now what `FactoryMembers` says. Where the factory would own the whole line, so that the input sits at the miner, the tests that need the start downstream of a merger or splitter put another factory's smelter at the line's head, so the line isn't owned. That's how those cases arise in a save, too.

## C# points

| Here | In Java / TypeScript |
|---|---|
| **`Func<double>` handed to `IncomingItem`**: `Of` passes `() => CouldBringMore(…)`, a closure over the world, the readings and the local functions `Spare` and `Sunk`. `Short()` calls it each time. | A `Supplier<Double>` from a lambda; TypeScript `() => number`. The closure captures the same variables. |
| **`Short()` is a method** because properties should be cheap: hovering in the debugger or serializing would otherwise run a maximum flow. Primer: *.NET basics → A method, not a property, for work that costs something*. | Java has no properties, so `getShort()` and `computeShort()` look alike. |
| **`[SuppressMessage("Naming", "CA1720…", Justification = …)]`** on one member. Primer: *.NET basics → CA1720*. | `@SuppressWarnings("…")`, without the category and the justification. |
| **A `public` class with an `internal` constructor** (`IncomingItem`): other assemblies read it, only `World.Domain` makes one. Primer: *.NET basics → A `public` class with an `internal` constructor*. | A package-private constructor. |
| **`private readonly record struct Source(…)`** as the key that sources are added up under, in place of a four-part tuple. Primer: *.NET basics → `readonly record struct`*. | A private `record` key. |
| **`Comparer<BuildableId>.Create((a, b) => string.CompareOrdinal(a.Id, b.Id))`** for a `SortedSet<BuildableId>`. | `Comparator.comparing(BuildableId::id)`; Java's `String.compareTo` is already ordinal. |
| **`git mv` of `InputSourceKind.cs` to another project** changes its namespace and visibility, not its JSON: the API writes enums by member name, camel-cased (`JsonStringEnumConverter` in `Program.cs`), so the namespace never reaches the wire, and the generated OpenAPI document didn't change. | Moving an enum between Maven modules; Jackson writes `name()` as well. |

## Tests

- Red first, one slice at a time:
  - The first slice didn't compile (`FactoryHandovers` and `IncomingItem` didn't exist yet). Then `Of` with arriving and need made it pass.
  - The second slice failed at compile time as well (`IncomingSource` and `InputSourceKind` weren't in the domain yet). After that, one test failed for a reason worth knowing: a loose belt that only feeds our smelter belongs to the factory, so it's no input at all. That test now shares the belt with another factory's smelter.
  - The third slice (short) didn't compile without `Short()`. I wrote the move of the maximum flow before running it, so that red wasn't run on its own.
- **Mutation check:** I switched off "its own machines are no outside supply" and "what reaches a sink is sunk". Three tests failed (the own-machines test and the two sink tests). Then I reverted both changes.
- `dotnet build SatisfactoryLedger.slnx`: 0 warnings, 0 errors.
- `dotnet test SatisfactoryLedger.slnx`: 1,308 passed, exit code 0. That's 1,298 − 8 deleted + 18 new.
  - On the first full run, `ArrangementSolverTests.A_placement_asked_to_avoid_earlier_ones_puts_a_factory_elsewhere` failed after 15 s. It's a time-budgeted solver test that this step doesn't touch. It passed alone in 0.6 s and in the full run after that.
- The golden fixtures, `FactoryInputsTests` and `FactoryOutputsTests` didn't change.

## Files

- **Domain:** `FactoryHandovers.cs`, `IncomingItem.cs`, `IncomingSource.cs` (new); `InputSourceKind.cs` (moved from `World/Analysis`); `InputSources.cs` (`CouldBringMore` and `MaximumFlow` removed)
- **Module:** `Analysis/FactoryInputs.cs` (now a projection), `Analysis/InputSource.cs` (reuses the domain enum), `Factories/FactoryTraces.cs` (preview reads `FactoryHandovers.Arriving`)
- **Tests:** `World.Domain.Tests/FactoryHandoversTests.cs` (new), `InputSourcesTests.cs` (lambda tests deleted)
- **Docs:** this log, `docs/domain/glossary.md`, `docs/guide/primer.md`
