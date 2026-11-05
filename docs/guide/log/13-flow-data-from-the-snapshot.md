# Step 13 · Flow analysis, part 2a: the data, from the snapshot

**Goal:** run the flow simulation of step 12 on a real world. That needs rates: what machines ask and make, what extractors offer, and what belts and pipes carry. The snapshot now copies these from the Catalog, and `SnapshotDocument.ToFlowNetwork()` gives the network.
**Result:** your test world runs end to end, from the save through the snapshot to the findings, and the numbers are right. Your big world runs in **3 seconds**: 1,016 parts, all settled but one, which is marked approximate. Solution total: 456 tests, all green.
**Decisions you made:** options 1 + 2 for parts that don't settle (BR-A10 amended). An exact LP solver goes on the [enhancements pile](../../domain/possible-enhancements.md). Part 2 is split as announced: **2a** the data (this step), **2b** ports, smart-splitter rules and merger priorities, **2c** stock and stations.
**Decisions I made, for you to overrule:** see "Choices worth a second look".

## What was built

```
Catalog.Contracts   GetExtractorsAsync, GetTransportTiersAsync; CatalogItem.IsFluid
SharedKernel        Rate * decimal (purity, somersloops), Rate.ToDouble()
World.Domain        Machine(id, Recipe?, ClockSpeed, productionBoost): Demand, Production   (BR-A1)
                    Provider(id, ItemOffer?): ItemOffer.Extracting(rate, purity, clock)    (BR-W9)
                    Logistics.Capacity; LogisticsKind.FluidBuffer; WorldGraph.Connections
World.Domain.Flow   WorldFlow.From(world, fluids): buildable → flow node
                    Carries (Solids / Fluids) on conduits, splitters, mergers, unknown sources
                    SteadyState: solids and fluids divided separately; a topological order
World.SaveFile      SavedProvider.Resource: a water extractor pumps water
World (snapshots)   SnapshotDocument version 2: extractors, transport tiers, nodes, fluids;
                    ToFlowNetwork(); version 1 still read
```

## How it works

**At import** (`ImportSaveHandler.CopyCatalogAsync`), besides the recipes, the World asks the Catalog for:
- the extractors of its providers;
- the tiers of its belts, lifts and pipes;
- the nodes its providers stand on;
- which of the items it uses are fluids.

Only what the save uses is copied into the snapshot (ADR-0016, amended). A snapshot keeps meaning the same thing after a later Catalog import.

**When building the world** (`BuildableDocument.ToBuildable`), the rates are computed **exactly, in the domain**:
- a machine asks recipe rate × clock speed, and makes that × production boost (`Machine`, BR-A1);
- an extractor offers extraction rate × purity × clock speed (`ItemOffer.Extracting`, BR-W9);
- a belt carries its tier's capacity.

Only `WorldFlow.From` turns them into `double`s for the simulation (ADR-0004, amended). An extractor whose node the Catalog doesn't know has no offer, so it becomes a source of unknown rate (BR-A9).

**Which node each buildable becomes** is a domain rule with its own tests (`WorldFlowTests`). A container at the end of a line is a sink, inside a line it passes items through, and fed by nothing it's an unknown source. The table is in `Flow/README.md`, section "From the world to the model".

## Two things the real worlds showed

### 1. Water on a conveyor belt

The first end-to-end run gave a packager at 100% and a water extractor delivering **0**. The container next to the packager "supplied what's asked": canisters *and* water, over its belt. The simulation knew no forms.

The fix is a general rule, now part of BR-A3: **belts, lifts, splitters, mergers and containers carry solids; pipes, junctions and fluid buffers carry fluids.** A connection carries what both its ends allow, and a machine or sink lets its line decide. Four places in the solver use it (README section 2b). As a bonus, a refinery's plastic now goes over its belt and its residue through its pipe. A residue pipe going nowhere blocks the refinery, on the residue, as in the game. That was limitation 2 of step 12.

The test world now gives:
- constructor C (underclocked) takes 22.5 ingots a minute;
- smelter B makes 10 of them at 100%;
- smelter A makes the other 12.5, at 41.7%, output blocked;
- the miner delivers 22.5 of its 60;
- the packager takes 60 m³ of water from the extractor and its canisters from the container.

`TestWorldFlowTests` checks all of these.

### 2. Your big world

| | |
|---|---|
| Buildables | 2,425 machines, 288 providers, 546 consumers, 38,700 logistics, 44,618 connections |
| Import | 12.8 s (save reading and storing) |
| Network from the snapshot | 0.6 s |
| One sweep | about 0.1 s |
| Settled | **no** |

Two separate problems, each found with a temporary probe that has been deleted:
- **Two blenders flip-flopped** between two states. The order of the passes was breadth-first, so a node reached by a short and a long path worked with numbers one sweep old. The order is now **topological**: a node after everything that feeds it. The flip-flop is gone. The test `A_branch_that_rejoins_the_line_settles_in_one_sweep_and_two_that_confirm_it` fails with the old order (4 sweeps instead of 3).
- **A slow drift remains.** I exported the 313-node part where it happens as a test network: no loop, and it drifts in 0.5 s just the same.
  - **What's happening:** four smelters of an iron-wire line share one ore splitter and meet again at three mergers. The ore supply and the room downstream bind at almost the same point: what's offered is only 0.00015/min more than what flows.
  - **Why it doesn't settle:** many splits of the ore over the smelters are then nearly valid. Each sweep moves the split by that slack, 0.00045/min, toward one corner, which it would reach after some 10,000 sweeps.
  - **How far off:** in such a part the flows can still move by a few items a minute on their way to that corner. Elsewhere they hold still. The simulation can't say it settled, and with 50,000 sweeps at 0.1 s it would run for over an hour.

**Also visible:** 1,290 of the 2,425 machines show as *starved*. Your world has **280 train and drone stations**, and the adapter doesn't model them yet (step 08). So every line a train unloads onto starts at nothing. That's increment 2c, and until then the findings on the big world are mostly about that.

## Your decision: what to do with parts that don't settle

1. **Solve each connected part on its own, and say per part whether it settled** (my recommendation):
   - Your world is hundreds of separate factories. Most settle in a few dozen sweeps, so the whole run gets much faster.
   - A part that hasn't settled after a cap (say 2,000 sweeps) is reported as **approximate**, with its numbers.
   - Together with option 2.
2. **Stop at BR-A10's tolerance:** stop when no flow changes by more than **0.0001/min** per sweep, as the rule says, instead of the solver's 10⁻⁹ now. That's a correction either way, but on its own it doesn't stop this drift (0.00045).
3. **A different solver:** compute the steady state as one system (linear programming with the even-split rules as constraints). It's exact and always ends, but it's a lot of work, and the even split of splitters and mergers is hard to write as constraints.

The domain question behind it: when a part has several near-valid steady states, is "approximately, and we say so" good enough for a diagnosis tool? I think yes: the findings there (*output blocked* at 59.7% or at 59.8%) mean the same thing to a player.

## After your decision: parts, and BR-A10's tolerance

You chose 1 + 2:
- **Parts.** `SteadyState.Of` splits the network into its connected parts (union-find over the connections, ignoring direction), simulates each on its own, and merges the results. `FlowResult.Parts` and `PartOf(id)` say per part whether it settled. `Settled` means every part did.
- **Stop rule.** A part has settled when no flow changes by more than **0.0001/min** for two sweeps (BR-A10). The 10⁻⁹ stays only for comparing doubles inside the solver.
- **Limit.** At most `MaxSweeps` = **2,000** sweeps per part. After that the part is *approximate*.

On your big world:

| | |
|---|---|
| Parts | 1,016; the biggest has 15,250 buildables |
| Sweeps | median 2, most 2,000 (the one approximate part) |
| Simulation | **3.0 s** for the whole world |
| Approximate | 1 part of 313 buildables: the iron-wire line from before |

The loop test shows the stop rule: 21 sweeps with the tolerance, 38 with 10⁻⁹, and still 2/3 to 0.0001.

## Tests (one behaviour each)

| Where | Tests |
|---|---|
| `RateTests` | A pure node doubles a rate · an impure node halves it exactly · a somersloop multiplies by a decimal · a rate becomes a double |
| `BuildableRatesTests` | Recipe rate × clock speed, for what a machine asks and makes · somersloops raise only what it makes · an unknown recipe asks and makes nothing · an extractor offers rate × purity (impure, normal, pure) · overclocked offers more |
| `WorldFlowTests` | Each kind of buildable becomes its node · a belt carries its capacity, or no limit if unknown · a container at the end, inside, or at the start of a line · belts carry solids, pipes fluids · a fluid buffer at the end and inside a line · a miner-to-sink world runs its smelter fully |
| `SteadyStateTests` | No fluid from an unknown source over a belt · a machine takes its fluid from the pipe · each product over the line of its form · a fluid product without a pipe blocks the machine · a branch that rejoins settles in 3 sweeps · parts that don't touch settle on their own · a part over the limit is approximate while the others settle · settled at the tolerance, twice in a row |
| `SaveFileReaderTests`, `GameClassesTests` | A water extractor pumps water · a miner's resource is left to its node · fluid buffers are their own kind |
| `SnapshotDocumentTests` | Only the extractors, belts and nodes it uses are copied · a miner offers its node's ore · a water extractor offers water · an unknown node gives no offer · a belt carries its tier · a machine asks recipe × clock · it knows its fluids · version 1 still reads · it says version 2 |
| `TestWorldFlowTests` | The test world end to end: settles, constructor C, smelters A and B, the miner, the packager |
| `CatalogQueriesTests`, `WorldSnapshotApiTests` | Extractors and tiers by class name · an item says whether it's a fluid · an imported snapshot has the rates the analysis needs (real PostgreSQL) |

**The tests can fail:**
- with `CanCarry` always true, exactly the four solids-and-fluids tests failed;
- with the old order, exactly the order test failed;
- with the old threshold (10⁻⁹), exactly the tolerance test failed.

## .NET points worth noticing (all now in the primer)

| .NET | Java/Spring (TypeScript) | Note |
|---|---|---|
| `static Rate operator *(Rate a, decimal factor)` | No operator overloading; `rate.times(...)` | Only where the meaning is obvious. |
| `… ?? []` making an empty `Dictionary` | `Map.of()` | The target type decides what `[]` becomes. |
| A method `ClockSpeed()` next to the type `ClockSpeed` | Not allowed in the same way | Compiles: where a type is expected, only types are looked up. Renamed for reading. |
| `record SavedProvider(…, string? Resource = null)` | A second record constructor | A default on the last parameter keeps old calls compiling. |
| A missing JSON property stays `null` in a non-nullable list | Jackson does the same | Nullable reference types are compile-time only, so `FromJson` fills in what a version 1 document lacks. |
| CA1829/CA1826: `list.Count()` fails the build | (Sonar would warn) | Use `Count` / `Length` with warnings as errors. |

## Choices worth a second look

1. **An extractor on a node the Catalog doesn't know** becomes a source of unknown rate (BR-A9 now says so), not a provider offering 0.
2. **Pumps and valves** have no tier, so no limit. A valve's own limit (it's in the save) comes with 2b.
3. **A belt whose tier the Catalog doesn't know** has no limit, and nothing reports it yet. Your big world has 1,823 nodes without a limit. Most will be containers inside lines, pumps and valves, but I haven't checked which, so that check goes with the next big-world run.
4. **Fluid buffers** are now `LogisticsKind.FluidBuffer`, separate from storage containers. They're still not a candidate end of an *output belt* (BR-W12), because a belt can't end in one.
5. **A version 1 snapshot** is read, without rates. Importing the save again gives version 2.

## Next

- **2c, stations:** a train, truck or drone station becomes an unknown source per platform, with its stock, and containers get their stock (BR-A9: confirmed after 30 minutes). This matters most for your big world.
- **2b, ports:** smart-splitter rules, priority mergers, and machines with two solid products.
- **Then part 3:** findings over the API.
