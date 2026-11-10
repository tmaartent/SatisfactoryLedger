# ADR-0026: The steady state as a sequence of linear programmes, in World.Simulation

- **Status:** Accepted (formulation to be refined while building it)
- **Date:** 2026-11-10
- **Amends:** [ADR-0018](0018-analysis-as-a-separate-service-over-rabbitmq.md): what the two services share.

## Context
The sweep solver (`World.Domain/Flow/Solver.cs`, BR-A1–A10) repeats a backward and a forward pass until nothing changes. Two problems remain:
- **Balanced loops settle at any speed.** In the owner's big world, four Ficsonium encoder–collider pairs settle at 4–55% while the game runs them at 100%. Every speed is a steady state, and sweeps have no reason to prefer the top (BR-A10, known limit).
- **Slow parts drift** and are reported as approximate after 2,000 sweeps.

A linear programme that maximises flow finds the top of a loop directly. But the game's sharing rules aren't an objective. A splitter divides evenly *on its own* (BR-A4), whatever happens elsewhere, and a merger does the same for its inputs (BR-A5): that's a local equilibrium. A global max-min (progressive filling) breaks it. Take source 10 → splitter S1 → (sink a, splitter S2 → sinks c, d): the game gives a = 5, while max-min gives a = 3.33. Minimising each splitter's largest output (Σ tₛ) is exact for one splitter, but loses the even split a few levels deep.

## Decision
- **A sequence of LPs** (decided 2026-11-10). Each LP is solved with a guess, per splitter output and per merger input, of whether it's **capped** (held below its share by what lies beyond it) or not. Uncapped ones carry the same share, a linear equality; capped ones carry at most that share. After each solve the guess is corrected, and solving stops when it no longer changes. A part whose guess keeps changing after a limit of rounds is reported as approximate, as today.
- **The LP per round:**
  - Variables: the flow `f(e, i)` per connection and item, for the items that can reach that connection (by form and sort rules), and a utilisation `u(m)` ∈ [0, 1] per machine.
  - A machine takes `demand × u` of each input (at most that of a stocked input, BR-A17) and makes `production × u` of each product (BR-A1).
  - Conduits, splitters and mergers pass on what comes in, per item. A belt, lift or pipe carries at most its capacity (BR-A3).
  - A provider delivers at most its offer (BR-A2). A source of unknown rate delivers what's asked, in proportion to what its machines ask (BR-A9). A sink takes everything (BR-A7, A8).
  - Sharing: a level per splitter and per merger, with the equalities and bounds above (BR-A4, A5).
  - Priorities (priority mergers, overflow outputs, uploaders and fuel that take only leftovers, BR-A5, A8) as **staged objectives**: solve the higher stage, fix its result, solve the next.
  - **Three objectives per round**, each solved with the ones before it held (settled in part C, 2026-11-10):
    1. **What the sources push in:** provider deliveries, plus what sources of unknown rate are asked for. Machines are passive, as in the game: they run on what reaches them. Maximising their utilisation first would starve a sink that a splitter feeds alongside a machine.
    2. **The capped shares filled:** each takes all its far end allows, the game's min(take, share).
    3. **Every machine's utilisation:** this is what runs a loop no source pushes.
  - **Correcting the guess:** every share is written as `share − level + below = 0`. While a share is uncapped, `below` is fixed at 0; capping it frees `below` to grow. After objective 1, a share whose `below` has a positive reduced cost is capped for the next round: letting it fall below its level would deliver more, so its far end can't take the share. Shares are only ever capped, never uncapped, so the sequence ends within as many rounds as there are shares. *(Part C first read the equality's dual value. With the fixed `below` variable, the solver can't attribute that value to the flows' own bounds instead, which happened for the tiers.)*
  - **Tiers** (part C2): a group served only when every share above it is capped. A smart splitter's outputs are tiered, per set of items its rules send alike: main, overflow, then the same two into what takes only leftovers (uploaders, station fuel at an unknown rate; BR-A8 serves the overflow side before the uploader). A priority merger's inputs are tiered per priority, highest first. A tier waits through a variable `b` fixed at 0, with `Σ tier − b = 0`. When `b` has a positive reduced cost, the block costs delivery, so the shares above are capped. Capped shares are filled tier by tier.
- **Findings** (starved of, blocked on, bottlenecks) are read from the solved flows and from each machine's demand and room, instead of from the sweep's *offered* values.
- **Google OR-Tools, GLOP** (`Google.OrTools` 9.15), an open-source LP solver with a .NET API.
- **Where the code lives, as World.SaveFile does it:** the library stays out of `World.Domain`.
  - `World.Domain` keeps the model: `WorldFlow`, `FlowNetwork`, the node types, the parts, `FlowResult`.
  - `World.Simulation` becomes the solver: OR-Tools, `AnalysisDocument.Compute`, and reading the network document (`ToNetwork`). Only the analysis service references it.
  - `World.Contracts` holds the wire documents shared by World and the analysis service: `SnapshotImported`, `FlowNetworkDocument`, `AnalysisDocument` and the other documents, and `SimulationJson`. They are plain records, with their own copies of `Carries`, `MachineLimit` and `SortRuleKind`, written to JSON by name.
  - World writes the network document (`FlowNetworkDocument.From`, a C# 14 static extension member). The analysis service reads it.
- **The sweep solver is deleted** once the LP passes `SteadyStateTests` and matches it on the owner's big world. Until then the work lives on the branch `feat/lp-solver`, since an LP without a production caller can't sit on `main`.

## Alternatives considered
- **Progressive filling (global max-min):** one clean objective, but it breaks the game's local even split (the example above).
- **The LP for loops only, the sweep for the rest:** small and solves the loop problem, but keeps two solvers. Rejected by the owner.
- **Rules an LP solves directly** (global fairness or maximum throughput): simplest, but no longer what the game does.
- **HiGHS** (`Highs.Native`): faster on large LPs and very small, but far less used, with a thin C-style API.
- **OR-Tools in `World.Domain`:** the domain would carry a native library; ADR-0003 keeps it free of frameworks and libraries.

## Consequences
- The two deployables now share only `World.Contracts` (plain records). The computation runs in the analysis service alone (amends ADR-0018's "shared code").
- `SnapshotImported` changed namespace. Wolverine names messages by type, so a message still in the outbox under the old name isn't delivered. The startup requeue (ADR-0017) asks again for every snapshot without a current analysis.
- The analysis service carries OR-Tools' native runtime, tens of MB per platform.
- Results change where the sweep didn't settle, and in loops: a new analysis version, so every stored analysis is computed again.
- Convergence of the sequence isn't guaranteed, just as with the sweep. A part that doesn't settle stays *approximate* (BR-A10).
