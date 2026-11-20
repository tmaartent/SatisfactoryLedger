# Step 56 · Where a shortage starts

**Goal:** `trace_upstream` should check where the shortage starts (asked 2026-11-20); its mirror, `trace_downstream`, does the same for an output blockage. Branch `feat/mcp-server`, on top of step 55 (committed), uncommitted.

## What you decided

- **Where a shortage can start:** too few producers, a belt or pipe full, a source at its limit, taken by others first. Nothing bringing it, no recipe and unknown supply always count.
- **A trace starts from a building and recipe:** all the held-back machines of that kind, traced together.
- **It gives the starts with the path** leading to each.
- **Downstream in the same step.**

Written as **BR-A20** and **BR-A21** in `docs/domain/business-rules.md`.

## The rules, as built

**Upstream (BR-A20).** From the starved machines asked about, per item they're short of, the lines are followed upstream through logistics:

| Met on the way | What the trace does |
|---|---|
| A bottleneck belt, lift or pipe (BR-A12) | **beltFull**: a start; what's behind it isn't looked at |
| A producer that's starved itself | Followed in turn along what it lacks; a step is added to the path |
| Producers at full speed, sources giving all they can | If part of what they send ends **outside** the machines traced: **takenByOthers**, with who takes how much. Otherwise **tooFewProducers** (machines) or **sourceAtLimit** (miners, extractors) |
| A producer whose output is blocked | **producerOutputBlocked** (trace downstream from it to see why) |
| A station, or a container nothing feeds | **unknownSupply** |
| No source at all | **nothingBringsIt** (at the belt that starts at nothing), or **noRecipe** when a machine without a recipe stands at the line's start |
| A provider with supply to spare, and nothing else | **unclear** |

**Downstream (BR-A21)** is the mirror:
- **beltFull**;
- a blocked consumer followed in turn;
- **consumerStarved** (it lacks another input, with the share it gets);
- consumers at full speed that also take from other producers: **suppliedByOthers**; otherwise **tooFewConsumers**;
- **storageFull**, **nowhereToGo** (a belt to nothing, or a machine that doesn't take the item), **noRecipe**.

Machines short of the same item are followed **together**, so a line they share is walked once and gives one start. A machine and item are followed once, which keeps loops finite; past 12 steps the trace stops with **unclear**.

## How it's built

- **World.Domain, `Tracing/`** (no framework):
  - `ShortageTrace.From` and `BlockageTrace.From` walk the `WorldGraph`;
  - `FlowReadings` holds the analysis facts in domain types: machine readings, bottlenecks, spare supply, unknown supply, flows into logistics;
  - they give back `TraceStart`s: cause, item, where, path, rate, others, short of.
  - They reuse `OutputDestinations` (who gets what a producer sends) and `InputSources` (who sends what a consumer takes), the walks of steps 41 and 42.
- **World, `Tracing/`:**
  - `SnapshotTraces` reads the stored analysis into `FlowReadings`, picks the machines of the class and recipe asked, and groups every list of buildables by class and recipe, with the first ids and position.
  - `TraceEndpoints`: `GET /api/v1/world/snapshots/{id}/traces/shortage?building=…&recipe=…` and `/traces/blockage`, with 202 until the analysis is ready, like the findings. They're in `api/openapi.json`.
- **MCP:** `trace_upstream` and `trace_downstream` (`Tools/TraceTools.cs`).
  - They take a building and recipe by in-game name; a recipe the building doesn't have gets an error listing the ones it has.
  - They answer in in-game names, with positions in metres.
  - When none of the machines is held back, a note says so, instead of an empty list.
  - Each tool's description lists every cause, so the model knows what each means.

## Tests

1099 .NET tests green (1074 + 25), build clean. Written first and seen failing (not compiling, then wrong asserts of my own, see below).
- **Domain (16):** each cause in a world of a few machines, both directions. Also: machines sharing a line give one start, and only the held-back machines of those asked are traced.
- **World (5):** the test world's real analysis, which has two real cases:
  - the **iron rod constructor gets no ingots** at all: *nothingBringsIt*;
  - **smelter A is output blocked**, because constructor C, which takes its ingots, runs at full speed and also takes 10 a minute from smelter B: *suppliedByOthers*, with smelter B named.
- **MCP (4):** names both ways, the note, an unknown recipe, the tool list.

## Choices made (overrule if you like)

- **"Taken by others" wins over "too few producers"** when both are true: the answer names who else takes it, and the model can say both.
- **A provider with spare supply and no other cause** is *unclear*, not hidden: the trace doesn't guess at splitter dynamics.
- **12 steps at most**, and **10 ids** per group of buildings in an answer.
- **Generators** are producers (of their waste) and consumers (of their fuel) like machines; drone ports aren't traced.
- Not tried yet on your world.

## .NET points

| Point | Java / Spring |
|---|---|
| **A tuple with an array compares the array by reference.** My first asserts were `Assert.Equal((cause, ids), (start.Cause, start.At.ToArray()))`: always unequal, though both parts matched (primer, *Backend*). | A Java `record` holding an array has the same trap. |
| **`using X = Namespace.Type;`** (an alias) lets `SnapshotTraces` use the domain's `MachineLimit` next to the contract's enum of the same name. | Java has no import alias; you'd write the full name. |
| **Local functions capturing state** (`Follow`, `Missing` inside `ShortageTrace.From`) keep the walk's sets private to one call, without a class per trace. | A private method passing a context object, or a lambda held in a local. |
| **`List.RemoveRange`** undoes what a branch added when it turns out to sit behind a full belt: a cheap rollback without copying. | `list.subList(from, to).clear()`. |
