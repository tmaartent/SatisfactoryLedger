# Step 12 · Flow analysis, part 1: the steady-state simulation

**Goal:** the core of the diagnosis tool. Simulate what flows where in a steady state (BR-A1 to A10), and produce the first findings (BR-A11 to A13), as pure domain code tested on small hand-built networks.
**Result:** `SteadyState.Of(network)` in `World.Domain.Flow`. 21 tests, all green, plus an architecture test that namespaces follow folders. Solution total: 396 green. Each rule has its own tests, including your BR-S3 example: 60 ore for three machines that want 30 each gives 30, 15 and 15, not 20 each.
**Decisions you made:** the rules BR-A1 to A13. Containers at the end of a line take everything (BR-A7). Unknown sources supply what's asked, and are confirmed when their stock lasts 30 minutes (BR-A9, Q20). The simulation is approximate (ADR-0004 amended). This build order.
**Decisions I made, for you to overrule:** see "Choices worth a second look".

## What was built

```
World.Domain/Flow
  FlowNetwork      FlowMachine (demand and production per minute), FlowProvider (offer), FlowUnknownSource (stock),
                   FlowSink, FlowConduit (capacity), FlowSplitter, FlowMerger, plus connections
  SteadyState.Of   → FlowResult
  FlowResult       flow per connection and item · MachineState (utilisation, limit, item) · Bottleneck
                   · UnusedSupply · UnknownSupply (stock minutes, confirmed) · Settled, Sweeps
```

## How the simulation works

The full explanation, with two examples traced by hand, is in [`Flow/README.md`](../../../src/Modules/World/SatisfactoryLedger.World.Domain/Flow/README.md), next to the code. In short:

It alternates two passes until no flow changes by more than 10⁻⁹:

1. **Backward pass: what each receiving end can take.**
   - A sink takes everything.
   - A belt takes at most its capacity, and no more than what's after it takes.
   - A splitter takes what its outputs take together.
   - A merger's inputs share what it can pass on, evenly (BR-A5).
   - A machine takes of each input what its outputs and its scarcest *other* input allow. A belt bringing an item it doesn't ask for clogs: nothing comes in over it.
2. **Forward pass: what each sending end sends.**
   - A provider sends its offer.
   - A splitter or belt divides what comes in evenly over what its outputs take; what one output can't take goes to the others (BR-A4).
   - A machine runs at the lowest of *supplied ÷ asked* and *taken ÷ made* (BR-A1).

Next to what flows, each connection carries what its sender **offers**: what it would send if the receiving end took everything. That was the fix for the three tests that failed at first:
- **Starved or blocked:** back-pressure lowers what comes in as well. So the cause is read from what's offered: offered enough but the outputs take less means *output blocked*, too little offered means *starved*.
- **Two inputs of one machine:** each takes what the other input is offered, not what has arrived so far. Otherwise both wait for each other at the start and stay at zero.
- **Bottlenecks:** a belt at capacity with more offered upstream. Only the **first** saturated segment of a line counts, so ten Mk.1 segments are one bottleneck, not ten.

**Loops** simply take more sweeps. The loop test checks conservation. Ingots circling smelter → merger → splitter → constructor, with a belt back into the merger, can't disappear. So the smelter delivers exactly what the constructor takes: 20 of 30, which is 2/3, output blocked.

## Tests (one behaviour each)

| Rule | Tests |
|---|---|
| BR-A1 machines | 100% when supplied · half input → 50%, starved of it · output nowhere → 0%, output blocked · two inputs → the scarcest decides |
| BR-A2, A13 providers | Delivers what's taken, the rest is unused supply |
| BR-A3, A12 belts | At most its capacity · at capacity with more offered → bottleneck · below capacity → not · a chain of equal segments → one bottleneck · an output belt too slow for its machine → bottleneck, machine output blocked |
| BR-A4 splitters | Even split · what one can't take goes to the others · a manifold short of supply: 30 / 15 / 15 |
| BR-A5 mergers | Over capacity: even from each input · an input offering less leaves its share to the others |
| BR-A8 consumers | A sink takes everything |
| BR-A9 unknown sources | Supplies what's asked, assumed supplied · stock for exactly 30 minutes → confirmed · your packager's 751 canisters at 60/min → 12.5 minutes, assumed |
| Loops, BR-A10 | Conservation in a loop, and the result says it settled |

**The tests can fail:** with the first-segment filter switched off, exactly the chain test failed.

## After your review: namespaces follow folders

You spotted it: the files are in `World.Domain/Flow/`, but I'd declared them in `SatisfactoryLedger.World.Domain`. They're in `SatisfactoryLedger.World.Domain.Flow` now.

Nothing caught it, so I wanted the build to catch it next time:
- **IDE0130 (namespace doesn't match folder) doesn't run in a command-line build.** I raised its severity in `.editorconfig` and made `RootNamespace` and `ProjectDir` visible to the compiler, as Microsoft's docs say. A wrong namespace still built without a warning, so I took the compiler-visible properties out again. The severity stays, so your IDE shows it.
- **So `ArchitectureTests/SourceLayoutTests` checks it.** It reads every `.cs` file under `src/`, `tests/` and `tools/`, works out the expected namespace from the project and folder, and lists every mismatch.
- **It found two more:**
  - `SatisfactoryImporterTests` was in the `Importer` folder without `.Importer`. Fixed.
  - `ServiceDefaults/Extensions.cs` is in `Microsoft.Extensions.Hosting` on purpose (Aspire's template, so `AddServiceDefaults()` needs no `using`). It's a named exception in the test.
- **Checked:** with that test file's namespace put back, the test fails and names the file.

## .NET points worth noticing (all now in the primer)

| .NET | Java/Spring (TypeScript) | Note |
|---|---|---|
| `is FlowConduit { Capacity: < double.PositiveInfinity }` | Java 21 record patterns, without comparisons | A relational pattern on a property. |
| `MinBy`, `(double share, ItemId? item) = …` | `stream().min(comparing(…))`, no destructuring | Returns the element, not the key; tuples unpack, `_` discards. |
| `double.PositiveInfinity` as "no limit" | Same in Java | `Math.Min(60, ∞)` is 60. Subtracting two infinities gives NaN, so the solver avoids that. |
| IDE0130 only in an IDE | Checkstyle's package-name check runs in the build | Code-style rules that need project information don't all run in `dotnet build`. |
| `[GeneratedRegex]` on a `partial` method | `Pattern.compile` in a `static final` | A source generator writes the regex code at compile time: no parsing at runtime. |

## Choices worth a second look

1. **Not in this increment:** smart-splitter rules and priority mergers (the rest of BR-A4 and A5). They need to know which belt is on which port. Connections don't carry ports yet, so the next increment adds that in the adapter.
2. **A machine with more than one product** offers all of them on all its output connections. That's right for one output belt, but wrong for a refinery whose plastic goes on a belt and whose residue goes in a pipe. Ports fix this as well.
3. **Unknown sources supply the items asked downstream.** If they hold any of those items, only those. A station holding nothing right now still supplies what's asked.
4. **Settling:** at most 50,000 sweeps. A network that doesn't settle says so (`Settled = false`) rather than pretend.
5. **Not yet tried on your big world.** Speed on 42,000 buildables gets measured in the next step, when the snapshot provides the data.

## Next

- **Step 2, the data:**
  - extractor rates and node purity, copied from the Catalog into the snapshot;
  - belt and pipe capacities;
  - smart-splitter rules, merger priorities, and ports from the save;
  - stock from the save.

  Then building a `FlowNetwork` from a snapshot.
- **Step 3:** findings over the API, and a run on your big world.
