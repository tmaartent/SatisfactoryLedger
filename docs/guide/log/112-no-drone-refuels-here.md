# Step 112 · No drone refuels here

**Goal:** fix issue #44. A drone port with fuel in its slot where no drone refuels burns nothing, and the model now says so instead of treating it like a port whose rate isn't known. Branch `fix/no-drone-refuels-here`, from `main` after PR #45. The agent brief on the issue is the spec.

## Why

`DroneRefuelling.BurnRates` only returned the ports where drones refuel, at a known rate. Every other port got `BurnRate == null`, so two cases looked the same:

- a port where a drone refuels but hasn't flown yet: the rate **isn't known**;
- a fuelled port where **no drone refuels**: it burns **nothing**.

In the model both took their fuel from the line's leftovers (BR-A8), and since step 111 both listed the fuel in `Consumes`. In the game the second one's slot stays full and its belt backs up.

## What you decided (triage, 2026-11-26)

- **A state of its own**, not a rate of 0.
- **A fuelled port that no drone flies to at all** is the same case: no drone refuels here.
- **The drone-port findings** list such a port, saying "no drone refuels here".
- **In a blockage trace** (BR-A21) it's a start of its own, **no drone refuels here**, not "nowhere to go".
- **The shortage trace is unchanged:** an idle port asks for nothing, so it's never starved.
- **Drones that can't unload stay out of scope:** the latest-trip rate covers them, and it never drops to zero.

### Choices to overrule

- **At the simulation's edge, "no drone refuels" becomes a limit of 0.**
  - `WorldFlow.MostFuel` gives a flow node `BurnRate = 0`, so the solver and the LP cap the fuel at nothing, through the code that already handles a known rate.
  - The domain keeps the three states; only the flow nodes, which work in `double`s, see a 0.
  - The stored flow network keeps its shape, so `FlowNetworkDocument` stays at version 1. The analysis goes to version 16 (ADR-0017), so stored analyses are computed again.
- **`BurnRate` is gone from `Provider` and `Consumer`**, replaced by `FuelBurn`. A truck station, or a port whose drones haven't all flown, is `NotKnown`, which is also the default.
- **`DronePortFinding` gets both `Burns: double?` (null when idle) and `NoDroneRefuels: bool`**, so a client doesn't have to read meaning into a `null`.
  - The web has no drone-port findings page yet, so only its generated model changed.
  - Idle ports come after the ports with a known rate, sorted by id.
- **No guard in the solver's "asked" walk.** An idle port asks for its fuel at 0 there. A station upstream may then count that item among what it can send, but the 0 cap stops any flow. A test covers this.

## How it works

- **`FuelBurn`** (`FuelBurn.cs`): an abstract record with a private constructor and three nested sealed records:
  - `Known(Rate PerMinute)`
  - `NotKnown`
  - `NoDroneRefuels`
- **`DroneRefuelling.FuelBurns`** sets every fuelled port to `NoDroneRefuels` first. Each drone then changes the port it refuels at:
  - the first known rate makes it `Known`;
  - another known rate adds to it;
  - an unknown part makes it `NotKnown`.
- **`SnapshotDocument.DroneFuelBurns`** now runs even when no port has a drone, so fuelled ports in a world without drones are idle too. It reads the trip cost from the snapshot's one kind of drone port.
- **`Buildable.Fuelled`**: a port where no drone refuels consumes nothing. `Buildable.Burning` gives a demand only for `Known`.
- **`BlockageTrace.DownstreamLine`** collects idle ports that have the item in their slot, as `NoDroneRefuels` (`TraceCause.NoDroneRefuels`).
  - It also drops them behind a full belt, like the other ends.
  - The MCP server's term is `TraceCauses.NoDroneRefuelsHere`, also used in the `trace_downstream` description.
  - The web labels it "At", without a rate.
- **`SnapshotAnalysis.DronePorts`** adds the idle ports after the ones with a known rate.

## C# points

| Here | In Java / TypeScript |
|---|---|
| **`abstract record FuelBurn { private FuelBurn() {} public sealed record Known(Rate PerMinute) : FuelBurn; … }`**: a closed set; only the nested records can call the private constructor. | Java: `sealed interface FuelBurn permits Known, …` with records. TS: a discriminated union `{ kind: 'known', perMinute } \| …`. |
| **`(burns[refuelsAt], burnt) switch { (FuelBurn.NoDroneRefuels, { } rate) => …, (FuelBurn.Known sofar, { } rate) => …, _ => … }`**: a switch on a tuple, with type and property patterns per part. | Java 21's record patterns come close, but Java has no tuples: you'd switch on a small record. |
| **`case Consumer { FuelBurn: FuelBurn.NoDroneRefuels } port when port.Burns.Contains(_item):`**: a property pattern that tests the *type* of a property. | `c instanceof Consumer port && port.fuelBurn() instanceof NoDroneRefuels && …`. |
| **`FuelBurn (_) => new FuelBurn.NoDroneRefuels()`**: a lambda with its return type written out, so `ToDictionary` makes a `Dictionary<…, FuelBurn>`. Without it, invariance stops the assignment. | `Collectors.<…, FuelBurn>toMap(…)`, or a cast. |
| **`new FuelBurn.NotKnown() == new FuelBurn.NotKnown()`**: records compare by value, so a case without data needs no singleton. | The same for Java records. |
| **TypeScript `Record<TraceCause, …>`** in `trace-starts.ts`: after `npm run generate:api`, the new cause didn't compile until it had its label. | Java's `EnumMap` doesn't check that every key is there; a `switch` over an enum with no `default` comes closest. |

New in `primer.md`: **A lambda with its return type written out** (.NET basics). `FuelBurn` is a second example under **A closed set of outcomes as nested records**.

## Tests

Written first. Most failed to compile at first (`FuelBurn`, `FuelBurns` and the helpers didn't exist yet). The trace tests failed on behaviour: they got *too few consumers* and *nowhere to go*. The new MCP case failed until the term was there.

- **`DroneRefuellingTests`:**
  - 2 new tests: a fuelled port where every drone refuels at its own home, and a fuelled port without drones, are both "no drone refuels".
  - 7 existing tests now expect `FuelBurn`.
- **`BuildableRatesTests`** (2): a loading and an unloading port where no drone refuels ask for and consume none of their fuel.
- **`WorldFlowTests`** (3):
  - such a port takes none of its fuel, whether it unloads or loads;
  - a station sends it none. This one passed as soon as it was written: the 0 cap already does it.
- **`DroneSnapshotTests`** (2): a fuelled hub whose drone refuels at home, and a fuelled port in a world without drones.
- **`DronePortFindingsTests`:**
  - 1 new test: an idle port says so, has no burn rate and gets 0.
  - The order test now ends with the idle port.
- **`BlockageTraceTests`** (2): fuel for a loading and for an unloading port where no drone refuels starts there.
- **`TraceToolsTests`** (1 case): `trace_downstream` describes "no drone refuels here (" and not its code.
- **Web `trace-starts.spec.ts`** (1): the row reads "No drone refuels here", "At", with no rate.

Results:
- `dotnet test SatisfactoryLedger.slnx`: 1,279 passed, exit code 0.
- `ng test`: 457 passed, exit code 0.
- Lint is clean; the build has 0 warnings.

## Files

- **Domain:**
  - `FuelBurn.cs` (new), `DroneRefuelling.cs`
  - `Buildable.cs`, `Provider.cs`, `Consumer.cs`
  - `Flow/WorldFlow.cs`, `Flow/Objects/FlowSink.cs` (doc)
  - `Tracing/TraceCause.cs`, `Tracing/BlockageTrace.cs`
- **Module:**
  - `Snapshots/SnapshotDocument.cs`, `Snapshots/BuildableDocument.cs`
  - `Analysis/SnapshotAnalysis.cs`, `Analysis/DronePortFinding.cs`
- **Contracts:** `AnalysisDocument.cs` (version 16)
- **MCP:** `Answers/TraceCauses.cs`, `Tools/TraceTools.cs`
- **API:** `api/openapi.json` (regenerated)
- **Web:** `world/explain/trace-starts.ts`, `trace-starts.spec.ts`
- **Tests:**
  - `DroneRefuellingTests.cs`, `BuildableRatesTests.cs`, `WorldFlowTests.cs`
  - `TestWorld.cs` (two helpers)
  - `Tracing/BlockageTraceTests.cs`
  - `World.Tests/DroneSnapshotTests.cs`, `DronePortFindingsTests.cs`
  - `Mcp.Tests/TraceToolsTests.cs`
- **Docs:**
  - `glossary.md`: **No drone refuels here**, Cause, Refuels at, Demand, Burn rate
  - `business-rules.md`: BR-A8, BR-A16, BR-A21
  - `primer.md`
