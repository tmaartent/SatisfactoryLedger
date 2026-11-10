# Step 29c2 · The LP solver, part C2: tiers

**Goal:** the rules where one group gets something only after another can take no more. That's smart and programmable splitters (BR-A4), priority mergers (BR-A5), uploaders and a station's fuel at an unknown rate (BR-A8), and drone ports with a known burn rate (BR-A16). Same round structure as part C. Not wired in. Branch `feat/lp-solver`.

## How it works

- **Sort rules decide which items reach an output** (`SortRules`, used by `ItemsOnConnections`): the item itself, *any*, *any undefined*, or *overflow*. An output set to *none* gets no flow variable at all.
- **Tiers:** a sharing group can wait for the groups above it. While any share above isn't capped, the tier carries nothing. When that block costs delivery, the shares above are capped, and the tier is free next round.

| At | Tiers, in the order they're served |
|---|---|
| Smart splitter, per set of items the rules send alike | main → overflow → main into leftovers-only → overflow into leftovers-only |
| Plain splitter | its outputs → those into leftovers-only |
| Priority merger | its inputs per priority, the highest first |

- **Leftovers only** (`LeftoversOnly`): a connection whose every way on ends at an uploader, or that brings a station only the fuel it burns at an unknown rate.
- **Burn rates:** a drone port whose rate is known takes at most that of its fuel, and a source of unknown rate is asked for it.
- **Capped shares are filled tier by tier,** so a main output is filled before an overflow output.

## One fix worth knowing about

The first version read the **dual value** of the blocking constraint `Σ tier = 0`. It came out zero, and lower tiers never opened. The constraint duplicated the flows' own lower bounds of 0, so GLOP attributed the value to those bounds (the flows' reduced costs). That's a legitimate choice in a degenerate LP.

The fix was to give each decision **its own variable**:
- a tier waits through `b` fixed at 0, with `Σ tier − b = 0`;
- a share is `share − level + below = 0`, with `below` fixed at 0 while it's uncapped. Capping it frees `below`, which makes the share "at most the level".

Each fixed variable's **reduced cost** is exactly what lifting it would deliver. Positive means it helps (for a maximisation). Trying `Math.Abs` first capped two shares whose cost was negative; the sign matters.

## Files

| Where | What |
|---|---|
| `World.Simulation/SortRules.cs` | Where a smart splitter sends an item |
| `World.Simulation/LeftoversOnly.cs` | Connections that lead only to leftovers |
| `World.Simulation/SharingGroup.cs` | Now with `Items`, `Tier` and `Above` |
| `World.Simulation/LinearProgramme.cs` | Slack variables for shares and tiers, burn rates, filling per tier |
| `World.Simulation/LinearSteadyState.cs` | The tiered groups |
| `World.Simulation/ItemsOnConnections.cs` | Sort rules; burn rates as what's asked |

## Points worth noticing

| OR-Tools / LP | Java | Here |
|---|---|---|
| `Variable.ReducedCost()`: what one more unit of a variable would add to the objective. A variable fixed at 0 turns it into "what would lifting this rule deliver" | The same in OR-Tools' Java API | `LinearProgramme.SharesHoldingBack` |
| **Degeneracy:** when several constraints hold at once, the duals aren't unique. Ask the question through a variable of its own, not a constraint that overlaps others | — | ADR-0026 |
| A local function capturing a pattern variable (`merger`) of the enclosing `switch` case | Lambdas only | `PriorityOf` in `LinearSteadyState.SharingGroups` |

## Tests (19 new)

Overflow gets only what the others can't take · *none* gets nothing · two *any* share evenly · item rules sort a mixed belt · *any undefined* takes the unnamed items · a priority merger takes its highest input first · tops up from a lower one · equal priorities share · an uploader behind a smart splitter gets what the factory leaves · behind a plain splitter, what a machine short of coal leaves · at the end of a line it takes everything · a drone port takes only leftover fuel · a loading station too · a drone port with a known rate takes no more · shares the line evenly with the other consumers · a loading port with a known rate · a source of unknown rate sends a port the fuel it burns · but not for a port whose rate isn't known · cargo for a station is taken like by any consumer.

Red first: 9 of 19 failed. 10 passed by luck, because pushing as much as possible gave the same answer in those cases. Then 5 failed until tiers waited through their own variable, and 2 when I compared the reduced cost without its sign. **Totals:** 46 LP tests, 802 .NET tests.

## Still to come (part D)

- **Clogging:** a belt bringing an item a machine doesn't ask for stops. Today that item simply isn't sent there.
- A source of unknown rate sending in proportion to what's asked; a machine's products over several outputs, evenly.
- **Findings** (starved of, blocked on, bottlenecks, unused supply), `FlowResult`, solving per part. Then all of `SteadyStateTests` against the LP, the comparison on your big world, wiring it in, and deleting the sweep.
