# Step 29c · The LP solver, part C: sharing evenly, one splitter at a time

**Goal:** splitters and mergers share evenly each on their own (BR-A4, A5), with the sequence of linear programmes you chose (ADR-0026). This part covers plain splitters and mergers, belts fed by several connections, and several belts into one machine. Not wired in yet. Branch `feat/lp-solver`.

## How it works

Every node where connections share evenly is a **sharing group**:
- a splitter's outputs;
- the inputs of a splitter, merger or belt fed by more than one connection;
- per item, the belts that bring a machine that item.

Each group has a level `t`. A share that isn't capped carries exactly `t`; a capped one carries at most `t`.

```
round:   start with no share capped
  1. maximise what the sources push in         (providers, and what unknown sources are asked for)
     a share whose equality has a negative dual  → capped next round
     (lowering it would let more be delivered: what lies beyond it can't take its share)
  when no share holds the sources back:
  2. hold 1, maximise what the capped shares carry   (each takes all its far end allows)
  3. hold 2, maximise every machine's utilisation    (loops no source pushes)
```

**Why the sources push, rather than the machines pull.** I first wanted the dual to tell whether a share is capped, with "maximise utilisation" as the objective. Then a splitter feeding a sink and a machine would cap the sink, simply because the LP prefers to feed the machine. With the sources pushing, a share is capped only when capping it lets more be *delivered*. That happens only when its far end really can't take its share, which is the game's rule. Machines run on what reaches them.

**It always ends:** a share is only ever capped, never uncapped, so there are at most as many rounds as shares.

## Files

| Where | What |
|---|---|
| `World.Simulation/SharingGroup.cs` | Connections that share at one node, with the item for a machine's inputs |
| `World.Simulation/LinearProgramme.cs` | One round: variables, constraints, the three objectives, the shares holding back (duals) |
| `World.Simulation/LinearSteadyState.cs` | The sequence: rounds until nothing is capped any more, the sharing groups |
| `World.Simulation/ItemsOnConnections.cs` | Also how much each source of unknown rate is asked for (its limit in objective 1) |
| `docs/adr/0026-…` | The objectives and the correction rule, now settled |

## Points worth noticing

| OR-Tools / C# | Java | Here |
|---|---|---|
| `Constraint.DualValue()` after `Solve()`: for a maximisation, how much the objective rises per unit the right-hand side rises. **Negative** on `share − t = 0` means lowering the share would help | The same call in OR-Tools' Java API | `LinearProgramme.SharesHoldingBack` |
| One `Solver` per round with three solves: `Objective.Clear()`, new coefficients, solve again. GLOP starts from the previous basis | — | `Maximise`, `Keep` |
| Holding an objective: a constraint `≥ best − 10⁻⁹ × best`. At 10⁻⁷ the penalty in the next stage spent the slack: 19.999997 instead of 20 | — | `LinearProgramme.Keep` |

## Tests (10 new, one behaviour each)

Divides evenly when every output takes its share · what one output can't take goes to the others · a manifold short of supply: 30, 15, 15 · **each splitter on its own: a = 5, c = d = 2.5** (the counter-example to global fairness) · short of supply, a sink and a machine fed alike · an output into a belt that ends takes nothing · a merger over capacity takes evenly · an input offering less leaves its share · a merger whose output takes everything takes all its inputs bring · two belts into one machine share what it asks.

Red first: 7 of the first 8 failed (one passed by luck: the LP happened to pick the right corner). The two I added afterwards were cases a lucky pass could hide: one failed on precision only, and the slack was tightened. The architecture rule now looks for OR-Tools in `LinearProgramme`. **Totals:** 783 .NET tests.

## Not yet (part C2)

- **Smart and programmable splitters** (BR-A4): items only to the outputs their rules name; *overflow* outputs only what the others can't take; *none* nothing. Today they share like plain splitters.
- **Priority mergers** (BR-A5): the highest priority first.
- **Leftovers only** (BR-A8): uploaders, and the fuel of stations whose rate isn't known. A drone port with a known burn rate takes at most that (BR-A16).
- **Clogging:** a belt bringing an item a machine doesn't ask for stops. Today that item just isn't sent there.
- **Proportions:** a source of unknown rate sending each item in proportion to what's asked (BR-A9).
- A machine's products over several outputs, evenly.

## Choices worth a second look

1. **Sources of unknown rate push too,** up to what their machines ask. That's needed so they take part in sharing like providers. A station feeding only a sink pushes nothing: nothing asks for its cargo.
2. **Capped shares are filled together** (objective 2 maximises their sum), so two capped shares of one splitter aren't made even among themselves. It hasn't mattered in any test yet; a fairness round for them would fix it.
