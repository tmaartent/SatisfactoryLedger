# Step 34 · What the save shows leaving isn't blocked (BR-A7)

**Goal:** the cooling system blender `Build_Blender_C_2147385536` came out in the model at 68%, output blocked, while its output slot in your save is empty. Branch `feat/leaving-is-not-blocked`.

## What the model saw

16 blenders make 36 cooling systems a minute. Everything they reach (supercomputer assemblers, turbo motor and Thermal Propulsion Rocket manufacturers, all at 100%) takes 35.04. In a steady state the 0.96 surplus has to back up, and the model put it on two blenders. In your save the surplus is still filling the belts: 1117 cooling systems sit on that line, and every blender's output slot is empty.

## Your rule (decided 2026-11-10)

**Trust the save:** a machine with room in its output slot is never output blocked by that product, wherever it goes. The analysis then shows "now" for such machines rather than where they'll settle once the belts are full.

**Backing up**, refined the same day: a backed-up machine fills its slot, waits until one batch fits again, and fills it. So its slot always sits between the stack less a batch and the stack. Only **room for more than one batch** shows a product leaving. Your power shard encoder had 99 of 100 shards waiting with a batch of 1: backing up, as in your game.

## In the code

- `Machine.StackSizes`: per product, from the Catalog. The snapshot now copies the stack sizes of every item the world's recipes make, not just of what's held.
- `Machine.Leaving`: the products it shows leaving; `ShowsLeaving` now wants room for more than one batch (the step 30 evidence for full stations uses the same rule).
- `FlowMachine.Leaving`, on the wire too. In the sweep, `HoldingBack` leaves those products out of a machine's output room and its blocked products.
- BR-A7, glossary *Leaving / backing up*, analysis version 12.

Only a snapshot imported from now on has the products' stack sizes. Older ones keep the previous behaviour until the save is imported again.

## On your world (snapshot `01a1c74a…`)

| In the model | Step 33 | Now | Your game |
|---|---|---|---|
| Cooling system blenders (16) | 14 at 100%, 2 at 68% blocked | **all at 100%** | output slots empty |
| Power shard encoder `…2146425563` | 47%, blocked on shards | **38%, blocked on shards** | ~56%, blocked on shards |
| Aluminium loop B refineries | 24% | **100%** | run, a bit blocked |
| Machines running / output blocked / starved | 2103 / 213 / 108 | **2218 / 79 / 127** | |

Starved went up by 19: machines that now run use more of their inputs, and other machines on the same lines get less. Worth a look when one of them looks odd in your game.

## The test world

Its smelter A (98 of 100 ingots waiting, a batch of 1) now runs; it was output blocked at 41.67%. Three API integration tests now look at the rod constructor (starved, 0%) instead, count 4 running, and have the miner deliver 40 of 60.

## Tests

Written first:
- `SteadyStateTests`: a machine the save shows with room for its next batch isn't output blocked.
- `WorldFlowTests`: its products are leaving; with a full slot, nothing is; with room for just one batch, it's backing up.
- Snapshot: a machine knows how many of its products fit in a slot. The wire document keeps what's leaving.

**Totals:** 834 .NET tests.

## Points worth noticing

| | Java | Here |
|---|---|---|
| A computed property instead of a stored one | A getter that computes | `public IReadOnlySet<ItemId> Leaving => Produces.Where(…).ToHashSet();`: an expression-bodied property, computed on every read |
| A local helper in a list expression | A private method returning a list | `private List<ItemId> HoldingBack(…) => [.. machine.Production.Where(…).Select(…)];`: a collection expression as the whole body |

## Still open

- The encoder at 38% in the model against ~56% in your game: how many shards the encoders further on take (idle machines accepting inputs they can't use).
- How fast vehicles empty a station (enhancement pile).
