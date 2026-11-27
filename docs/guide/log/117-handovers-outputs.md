# Step 117 · A factory's handovers, the outputs side

**Goal:** issue #53, the third ticket of card 3 (#50). The module `FactoryHandovers` in `World.Domain` now also answers, per item, what a factory makes and where it goes. The rules that lived in the API layer's `FactoryOutputs` moved in. No behaviour changes. Branch `ticket/53-handovers-outputs`, from `refactor/factory-handovers`.

## Design

- **Module:** `FactoryHandovers` (`World.Domain`), the code home of **Handovers**. This step adds its outputs side, so one module covers both sides of a factory (decision 4).
- **Interface, what changed:**
  - `FactoryHandovers.Of(world, readings, members, owners, sinks, usedUp, buildingOf)`: `usedUp` joins here, the consumers that use up what they get (a portal, an alien power augmenter). It's a set of ids, as `sinks` is.
  - `Outputs` is a list of `OutgoingItem`, ordered by item id. Each has `Item`, `Now`, `AtFullSpeed`, `ReceiversNeed`, `OthersBring`, `Short`, `Sunk`, `CannotLeave` and `Destinations`.
  - An `OutgoingDestination` has `Kind`, `Owner` (a `Guid`, decision 7), `Building` and `PerMinute`.
  - All values are unrounded (decision 9).
  - `FactoryHandovers.Leaving(world, readings, output, item)` tells what leaves over one output: the outputs-side twin of `Arriving`. The factory preview calls it, and so does `Of`.
  - `FactoryHandovers.Ends(world, readings, members)` gives what reaches each end of the factory's lines, per item, in the order its outputs come. The arrangement calls it, and so does `Of`.
- **The rules moved in:**
  - what would leave at 100%: what its machines make, less what its own machines take;
  - a product its own machines take all of doesn't leave;
  - the kind of each destination;
  - what reaches a sink is sunk;
  - what this factory brings its receivers;
  - receivers' need, others bring and short;
  - what an output-blocked producer can't get out.
- **What became private:** `OutputDestinations.ReceiversNeed` and `ReceiversGet` (decision 11). Their walk (`Receivers`) and `Asks` are private parts of `FactoryHandovers` now. `OutputDestinations` keeps only the line-following `Of`, which the traces call too.
- **Moved:** `OutputDestinationKind` moved from `World.Analysis` to `World.Domain`. It was `internal` and is now `public`. The API record `OutputDestination` reuses it, and its JSON names don't change.
- **Facts from the caller, one copy:** the new `SnapshotHandovers` (`World/Analysis`) reads the facts from the snapshot: the owners, the sinks, the consumers that use things up, and each building's class. Both projections call it. `SinkClass`, `UsesUp` and `OwnersIn` moved there from `FactoryOutputs`. In step 116 `FactoryInputs` reached into `FactoryOutputs` for two of them.
- **Projection:** `FactoryOutputs.Of` is now a rounding projection of `Outputs`. It drops destinations that round to nothing and looks up the receiving factory's name by its `Guid`. `FactoryOutputs.Over`, `Ends`, `OwnersIn` and `SinkClass` are gone.
- **Deletion test:**
  - Delete `FactoryHandovers`, and the outputs rules go back into `FactoryOutputs`, with `Over` and `Ends` beside them for the preview and the arrangement.
  - Delete `SnapshotHandovers`, and the facts get built twice, once in each projection. Before this step one projection reached into the other.
  - `OutgoingItem` and `OutgoingDestination` exist so the projection doesn't reach into tuples.
  - Nothing new is test-only. Every public member has a production caller in `FactoryOutputs`, `FactoryTraces` or `WorldArrangement`.
- **Glossary:** the **Destination**, **Receivers' Need**, **Others Bring / Short** and **Sunk / Can't Leave** rows name `FactoryHandovers` as their one code home. The **Handovers** row describes both sides.

### Choices to overrule

- **The "ends reached per minute" are a static `FactoryHandovers.Ends`, not a field of `OutgoingItem`.** The ticket lists them among the outputs, and #54 removes `FactoryOutputs.Ends`. Two things led me here:
  - Reading ends per item from `Outputs` would put the arrangement's stations in item-id order. Today they come in the order the factory's outputs come.
  - That order reaches the arrangement problem: `fixedPlaces` and `spotRates`, and which station's entry is found first. So the solver could place things differently. That's a behaviour change, and decision 2 rules it out here.

  So `Ends` moved into the module as it was, and `FactoryOutputs.Ends` is deleted now. A pass-through would have been a shallow module. The arrangement calls `FactoryHandovers.Ends(world, readings!, members)`. That's a one-line change in `WorldArrangement`. #54 decides whether the arrangement can read per-item ends, and accept the new order.
- **`Leaving` takes one item and returns a `double`**, like `Arriving`, and no longer a dictionary per output. `FlowReadings.Carries(id, item)` is `Carrying(id).GetValueOrDefault(item)`, and the sums run in the same order, so every value is the same down to the last bit. `Ends` and `Of` now call it once per output and item, not once per output.
- **`OutgoingItem.Short` is a property, `IncomingItem.Short()` a method.** On the outputs side, short is need less what the receivers get, two numbers already worked out, so it's cheap. On the inputs side it's a maximum flow (decision 8). The name and the CA1720 suppression are the same as step 116's.
- **`SnapshotHandovers.NamesOf` names every stored factory**, this one too, and not only the owners found in the snapshot. A lookup by an owner's `Guid` gives the same name as before.
- **The two projections each call `SnapshotHandovers.Of`**, so a factory's handovers are worked out twice per `ForFactory`. That's cheap, because `Short()` only runs when read. Building them once in `SnapshotAnalysis` would change `FactoryInputs.Of`'s signature, which the arrangement also calls, and #54 rewrites that call anyway.

## Seams under test (decision 12)

- **A, the module through its interface:** `FactoryHandoversTests` gained 21 tests on the outputs side, through `Of` (and one through `Ends`) on a `TestWorld` with `TestReadings`:
  - what leaves now and at 100%, four tests: the plain case, what its own machines take, what they take all of, and into a container of its own;
  - destinations, seven tests: another factory, no factory, the AWESOME Sink (sunk), a consumer by building, storage, a belt to nowhere, most first;
  - receivers, eight tests: the six old `ReceiversNeed` cases rewritten, then others bring and short on one shared assembler (the old `ReceiversGet` case);
  - can't leave;
  - the ends per item.

  This seam catches the rules. It misses how the factory page rounds and names them.
- **B, the safety net, unchanged:** the `ForFactory` tests (`FactoryOutputsTests`, `FactoryInputsTests`) and the golden fixtures pass without an edit.
- **C, line-following:** the six `OutputDestinations.Of` tests stay as they were.
- **Replace, don't layer:** the seven `ReceiversNeed` and `ReceiversGet` tests are deleted from `OutputDestinationsTests`.

### How the old tests became module tests

| Before | Now |
|---|---|
| `ReceiversNeed(world, [Id("splitter")], …)`: the walk starts where the test says | Our constructor's belt leads to the splitter; the walk starts at the factory's outputs, as `FactoryMembers` gives them |
| A generator's fuel and water as two calls | Our manufacturer and refinery send rods and water; one assertion over both items of `Outputs` |
| `ReceiversGet(…) == 24` | `OthersBring == 14` and `Short == 6` on the same world: the 24 the assembler takes, less our 10, and its 30 less 24 |

A belt that only our machines use belongs to the factory, so it's no output at all, as step 116 found for inputs. The test of a belt that goes nowhere shares its merger with another factory's constructor for that reason.

## C# points

| Here | In Java / TypeScript |
|---|---|
| **`double @short`** as the constructor parameter behind `Short`: `short` is a keyword, and `@` makes it a plain name. Primer: *.NET basics → `@short`: a keyword as a name*. | Java has no escape; you'd rename it. |
| **`private readonly record struct Destination(Kind, Owner, Building)`** as the dictionary key that destinations add up under. It replaces a tuple holding a whole `StoredFactory`, so the domain never sees an API type. | A private `record` key. Its `equals`/`hashCode` come from the components in both languages. |
| **`git mv` of `OutputDestinationKind.cs` into another project**, as step 116 did for `InputSourceKind`: the namespace and visibility change, the JSON doesn't. The API writes enum member names, camel-cased. | Moving an enum between Maven modules; Jackson writes `name()` as well. |
| **`Aggregate(Rate.Zero, (sum, receiver) => sum + …)`** adds up exact `Rate`s and turns the total into a `double` once, as `ReceiversNeed` did, so the value doesn't change. | `stream().reduce(Rate.ZERO, (sum, r) -> sum.plus(…), Rate::plus)`. |

## Tests

- Red first, one slice at a time. Each red was a compile error first: `OutgoingItem` didn't exist yet, then `OutgoingDestination`, then `ReceiversNeed`, `OthersBring` and `Short`, then `CannotLeave` and `Ends`.
  - One test failed for a reason worth knowing: a belt that goes nowhere after our constructor belongs to the factory, so the factory has no output there. The test now shares the line with another factory.
- **Mutation check:** I switched off two rules: what this factory brings is no longer taken off, and a consumer that uses things up is no longer recognised. The two tests for them failed, and I reverted both changes.
- `dotnet build SatisfactoryLedger.slnx`: 0 warnings, 0 errors.
- `dotnet test SatisfactoryLedger.slnx`: 1,322 passed, exit code 0. That's 1,308 − 7 deleted + 21 new.
- The golden fixtures, `FactoryOutputsTests` and `FactoryInputsTests` didn't change.

## Files

- **Domain:** `FactoryHandovers.cs` (outputs side, `Leaving`, `Ends`, private `Receivers` and `Asks`); `OutgoingItem.cs` and `OutgoingDestination.cs` (new); `OutputDestinationKind.cs` (moved from `World/Analysis`); `OutputDestinations.cs` (`ReceiversNeed` and `ReceiversGet` removed)
- **Module:** `Analysis/SnapshotHandovers.cs` (new: the facts); `Analysis/FactoryOutputs.cs` (now a projection); `Analysis/FactoryInputs.cs` (gets its facts from `SnapshotHandovers`); `Analysis/OutputDestination.cs` (reuses the domain enum); `Factories/FactoryTraces.cs` (the preview reads `FactoryHandovers.Leaving`); `Arrangement/WorldArrangement.cs` (reads `FactoryHandovers.Ends`)
- **Tests:** `World.Domain.Tests/FactoryHandoversTests.cs` (21 new); `OutputDestinationsTests.cs` (receivers tests deleted)
- **Docs:** this log, `docs/domain/glossary.md`, `docs/guide/primer.md`
