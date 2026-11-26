# Step 113 · One reader of the stored analysis

**Goal:** candidate 2 of the architecture review of 2026-11-26. Every finding and trace reads the stored analysis through one reader, `FlowReadings`, instead of five places each reading `AnalysisDocument.Into` and `.Machines` their own way. Branch `refactor/analysis-reader`, from `main` after PR #46.

## Why

The stored analysis keeps only what flows into each belt, lift, pipe and container (ADR-0017). Everything else had to be worked out from that, and each finding did it its own way:

| Who | Read the analysis like this |
|---|---|
| The traces | `FlowReadings`, through `SnapshotTraces.Readings` |
| `FactoryInputs` | its own `Into`, `Carries` (through splitters and hubs, as far as they go), `Room`, `FlowOutOf`, `Arriving` |
| `FactoryOutputs` | its own `FlowInto`, `Intake` (step 111), `Over` |
| `SnapshotAnalysis` | `Supplied`, power, stock |
| `PipeFlows` | what a network takes in, one level up |
| `FactoryTraces`, `WorldArrangement` | through `FactoryInputs` and `FactoryOutputs`, with the raw document |

The copies had drifted apart:
- **A machine with no state:** the traces assumed it runs at 1, the factory findings at 0.
- **Splitters, mergers and pipe networks:** `FactoryInputs` followed them as far as they go; `PipeFlows`, `Supplied` and `Intake` went only one level up.
- **What reaches a buildable:** three versions. `Supplied` counted every connection into it, `Intake` only the logistics, and `FlowInto` what it takes at the rate it runs.

## What you decided (2026-11-26)

- **Q1:** `FlowReadings` is the one reader, built from the stored analysis by one adapter in production (`AnalysisReadings.Of`) and by hand in tests (`TestReadings`). It's built with the world, so it can answer for splitters, hubs and machines.
- **Q2:** what a piece of logistics **carries**:
  - a belt, lift, pipe or container carries what the analysis stored;
  - a splitter, merger, pipe junction or pipe network carries what the logistics feeding it carry, as far as they go.
- **Q3:** what **reaches** a buildable is what the logistics ending at it carry, not what it takes from its stock. One exception: a machine fed from a pipe network others share counts what it **takes** at the rate it runs.
- **Q4:** a machine or generator with no state runs at 0: it takes and sends nothing.
- **Q5:** the factory rules stay in `FactoryInputs`:
  - its own machines are no outside supply;
  - a station's spare comes from its stock lasting 30 minutes;
  - sources are grouped by factory or building.
- **Q6:** the solver-based findings tests stay unchanged, as the safety net. Moving them to hand-built readings is issue #47 (`needs-triage`).

### Choices to overrule

- **`FlowReadings` stays in the `Tracing` namespace**, where it started, though the findings read it too now. Moving it, to `World.Domain` itself or an `Analysis` namespace, would touch every `using`.
- **The reader holds the world.** Callers ask by id: `readings.OutOf(id, item)`, no longer `OutOf(buildable, item)`. The traces still take the world as a parameter of their own as well; folding that in would change about 40 trace tests for no change in behaviour.
- **A pipe network or junction counts as shared** when it feeds more than one buildable directly. Then each machine it feeds counts what it takes (Q3).
- **Two places still read `AnalysisDocument` directly**, because they list its own rows rather than read flows:
  - `SnapshotAnalysis.Findings` turns the stored machine states, unused supply and unknown supply into the response's lists;
  - `SnapshotTraces` picks the machines of a class that have a state.

## What changes in the model

None of the 1,279 existing tests changed result, and those include every findings test that runs the real solver. Three things can still read differently than before, all where the old copies disagreed:

- **A pipe network fed through a junction:** in the model it now shows all it takes in. Before, the network intake shown on a pipe, and what crosses a factory's output into storage, missed what came through the junction.
- **At a split in a trace (BR-A20):** a machine at the end of a branch now counts what it takes when shares are worked out. Before it counted 0. Splitters feed belts, so this only matters where a pipe network or junction feeds machines directly.
- **What arrives at a consumer from a pipe network, in a blockage trace (BR-A21):** a hub now carries what the pipes feeding it carry. Before it carried 0, so a consumer fed straight from a network could now count "supplied by others" where it said "too few consumers".

## How it works

- **`FlowReadings`** (World.Domain, `Tracing/FlowReadings.cs`): a class with a primary constructor that takes the world and the five stored tables. Besides `Machine`, `IsBottleneck`, `OfferedMore`, `Spare`, `IsUnknownSupply` and `Delivered`, it now answers:

  | Member | Answers |
  |---|---|
  | `Carries(id, item)`, `Carrying(id)` | what a piece of logistics carries (Q2), recursively through hubs, with a `passed` set against loops |
  | `Room(id)` | capacity less all it carries; no limit without a capacity |
  | `Takes(id, item)` | utilisation × demand; 0 without a state (Q4) |
  | `Reaching(id, item)` | what the logistics ending at it carry; for a machine on a shared pipe network, `Takes` (Q3) |
  | `Into(id, item)` | logistics: `Carries`; a machine or generator: `Takes`; anything else (a drone port, a station, a sink): `Reaching`. Used to share a line at a split, and for BR-A18's "get". |
  | `OutOf(id, item)` | what it sends on: logistics what it carries, a machine what it makes at the rate it runs, a provider what it delivers |
  | `MakesMore(id, item)` | what a machine would make more at 100% |

- **`AnalysisReadings.Of(world, analysis)`** (World, `Analysis/AnalysisReadings.cs`): the one adapter, moved out of `SnapshotTraces`.
- **The callers** each build the readings once and pass them on:
  - `SnapshotAnalysis.For`, `ForFactory` and `PowerOf`
  - `FactoryInputs.Of` and `Arriving`
  - `FactoryOutputs.Of`, `Ends` and `Over`
  - `PipeFlows.Of`, `FactoryTraces`, `WorldArrangement`
- **Deleted:**
  - `FactoryInputs`: its `Into`, `Carries`, `Room` and `FlowOutOf`.
  - `FactoryOutputs`: its `FlowInto` and `Intake`.
  - `SnapshotAnalysis.Supplied`, and `SnapshotTraces.Readings`.

## C# points

| Here | In Java / TypeScript |
|---|---|
| **`using MachineBuilding = SatisfactoryLedger.World.Domain.Machine;`**: a `using` alias. Inside `FlowReadings`, `Machine` means its method `Machine(id)` (CS0428, the step 111 quirk again), so the type gets another name. | Java's `import` can't rename. You'd write the full name. TypeScript's `import { Machine as MachineBuilding }` is the same idea. |
| **A primary constructor parameter (`world`) used in members**: the compiler keeps it as a hidden field, captured once. | A Java record component or a final field set in the constructor. |
| **`world.Find(id) switch { Logistics { Kind: … or … } when passed.Add(id) => …, Logistics => …, _ => … }`**: a property pattern with `or`, and a `when` guard that has a side effect (it marks the hub as passed). | `switch` with pattern guards in Java 21 (`case Logistics l when …`); `or` patterns have no Java equivalent. |
| **`IReadOnlyDictionary<ItemId, double>`** returned from a shared empty `Nothing`: one instance, never changed, safe to hand out. | `Map.of()` in Java. |

New in `primer.md`: the `using` alias, added to **A method hides a type of the same name**.

## Tests

The reader's tests were written first: they failed to compile (`Build(world)`, `Carries`, `Reaching` and the rest didn't exist). `FlowReadingsTests` has 15:
- **What logistics carries:**
  - a belt carries what was found flowing into it;
  - a merger carries what its belts carry;
  - a merger fed by a merger carries what both lines bring;
  - a pipe network fed through a junction carries what the pipes before the junction carry.
- **Room:**
  - a belt has room for its capacity less all it carries;
  - a splitter has no limit of its own.
- **What a machine takes and sends:**
  - a machine takes its demand at the rate it runs;
  - with no state, it takes and sends nothing;
  - it sends on what it makes at the rate it runs;
  - it would make more at 100% what it leaves unmade.
- **What reaches a buildable:**
  - what reaches a drone port is what the belts ending at it carry;
  - what reaches a machine over its belts leaves out its stock;
  - from a shared pipe network, what reaches a machine is what it takes.
- **What flows into a buildable:**
  - into a machine, what it takes; into a belt, what it carries;
  - into a drone port, what reaches it.

Other test changes:
- `TestReadings` takes the world (`Build(world)`), and `Into` adds an item to a belt instead of replacing what it had.
- The trace tests pass their world to it.
- `SnapshotTracesTests` reads through `AnalysisReadings.Of`.

`dotnet test SatisfactoryLedger.slnx`: 1,294 passed (1,279 before, plus 15), exit code 0. The build has 0 warnings. The web app is untouched: no contract changed, and the analysis version stays 16.

## Files

- **Domain:** `Tracing/FlowReadings.cs`, `Tracing/BlockageTrace.cs`, `Tracing/ShortageTrace.cs` (`OutOf` by id)
- **Module:**
  - `Analysis/AnalysisReadings.cs` (new)
  - `Analysis/FactoryInputs.cs`, `FactoryOutputs.cs`, `SnapshotAnalysis.cs`, `PipeFlows.cs`
  - `Factories/FactoryTraces.cs`, `Arrangement/WorldArrangement.cs`, `Tracing/SnapshotTraces.cs`
- **Tests:**
  - `Tracing/FlowReadingsTests.cs` (new), `Tracing/TestReadings.cs`
  - `Tracing/BlockageTraceTests.cs`, `ShortageTraceTests.cs`
  - `World.Tests/SnapshotTracesTests.cs`
- **Docs:**
  - `glossary.md`: **Carries / Reaches / Takes**
  - `business-rules.md`: BR-A17, the shared pipe network
  - ADR-0017: one reader, under Consequences
  - `primer.md`
