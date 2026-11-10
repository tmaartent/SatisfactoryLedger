# Step 29e · The LP starts the sweep

**Goal:** after D2's comparison, the decision changed (ADR-0026, at its end). The linear programme now gives the sweep a running start, and the analysis uses it (analysis version 7). The sweep stays. Branch `feat/lp-solver`.

## How we got here

1. **Confirming caps** (commit `fa201da`): the LP had sent a smart splitter's beams to its overflow. Caps are now confirmed by a what-if, and that turned up an OR-Tools trap: after any bound changes, `SolutionValue()` reads 0.
2. **Option 1, start with everything capped,** broke on paper. The game's rule, share = min(limit, level), is a complementarity condition that an LP can't choose by itself.
3. **The comparison fixture was stale.** `big-world-part.flow.gz` is from step 15 and has no uploaders taking only leftovers, among other things. Your game check (104 beams/min, the *any* output blocked by a full depot) showed it. On your **current** world the sweep had the heavy-frame chain right all along.
4. **On a current export** (World's internal flow-network endpoint), the sweep primed with the LP's running state:

| Your world, 42,517 nodes | Sweep | Sweep from the LP's start |
|---|---|---|
| Time | 5 s | about 1 s + 5 s |
| Every part settled | no (one of 313) | **yes** |
| Mean utilisation | 0.865 | **0.900** |
| Machines changed by more than 1% | | 211 higher, 64 lower |
| Aluminium smelters and casing assemblers | 0% | **100%**: confirmed in the game |

The pure LP failed numerically ("ABNORMAL") on one part of the current world. The running start is solved part by part: a part that fails starts from zero.

## What changed in the code

| Where | What |
|---|---|
| `World.Domain/Flow/Solver.cs`, `SteadyState.cs` | An optional `start`, a flow per connection, primed as if running |
| `World.Simulation/LinearSteadyState.cs` | `RunningStart`: one LP per part, every share free; push in, then utilisation |
| `World.Simulation/NetworkAnalysis.cs` | `Compute` starts the sweep from `RunningStart` |
| `World.Contracts/Analysis/AnalysisDocument.cs` | Version 7: every stored analysis is computed again |
| `docs/adr/0026-…`, `docs/domain/business-rules.md` (BR-A10) | The changed decision |

## Tests

- Domain: a sweep started from more than can flow settles on what does.
- Simulation: from the running start, the nitric acid loop keeps 100% when it gets its water back, and winds down when it gets too little.
- These were written after the experiment's code, so they pin it rather than lead it.

**Totals:** 875 .NET tests.

## Points worth noticing

| | Java | Here |
|---|---|---|
| An optional parameter added last (`start = null`) keeps every call compiling | An overload | `SteadyState.Of(network, maxSweeps, start)` |
| A fixture is a snapshot of the model at the time it was written | The same | Compare results on a fresh export, not on a test fixture |

## Still open

1. **The Ficsonium loops** barely change with priming: some *rule* holds them back, not the starting point. Next to trace, on the current export.
2. **BR-A7, decided:** a storage container at the end of a line, **and a loading freight station** (it has an inventory too), takes everything **until it's full**. One full at save time blocks its line: in your world, the aluminium casing assemblers behind a container and a freight station. It needs container and station sizes and stack sizes from the Docs file: a step of its own.
3. **Before merging `feat/lp-solver`:** the full LP (`LinearSteadyState.Of`, `LinearFindings`, the rounds, tiers and what-ifs) has no production caller any more, and goes (CLAUDE.md). Its tests go with it, and the sweep's tests no longer need linking into the Simulation tests.
