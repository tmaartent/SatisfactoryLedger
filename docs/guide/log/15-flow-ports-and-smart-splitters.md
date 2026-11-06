# Step 15 · Flow analysis, part 2b: ports, smart splitters, priority mergers and fluid stock

**Goal:** the rest of BR-A4 and A5. Smart and programmable splitters follow their rules, priority mergers their priorities. Both refer to ports, so connections need to know them.
**Result:**
- **Smart splitters:** every connection from a splitter or into a merger knows its port, and smart splitters follow their rules.
- **Your big world:** 952 machines at 100% (was 867), 802 starved (was 854). It runs in 11 s, with only the known iron-wire part approximate.
- **A third flip-flop**, only on the whole big world, needed damping. A regression test runs on that part of your world.
- **Priority mergers** follow their priorities. A **2 is the highest**: your test world told me, with smelter B on the center input.
- **Fluid truck stations** are stations, with their own fuel belt. **Fluid stock** is read, from your new test world.
- **A bug from step 14 fixed:** a building the adapter doesn't know, with an unknown port name, broke the whole import.
- **Your big world now:** 8.6 s, 931 machines at 100%, 14 of 50 sources of unknown rate confirmed.
- **Tests:** 512, all green.

**Decisions you made:** a priority merger serves its highest number first. You built the test case: the old merger replaced by a priority merger, the center input at highest priority.
**Decisions I made, for you to overrule:** see "Choices worth a second look".
**Spike:** a throwaway test on the test world and your big save, deleted afterwards.

## The spike: what the save says

*(The second half of this step, after your answer, is under "Priority mergers and fluids".)*

**Smart splitters** store `mSortRules`: per rule an `ItemClass` and an `OutputIndex`. The test world's smart splitter has three:

| Rule | `OutputIndex` | Port | Linked to |
|---|---|---|---|
| `Desc_Wildcard_C` (any) | 0 | `Output1` | belt → storage container |
| `Desc_Overflow_C` | 1 | `Output2` | belt → AWESOME Sink |
| `Desc_None_C` | 2 | `Output3` | nothing |

So `OutputIndex` *i* is port `Output`*i+1*. That's how you built it: overflow to the sink, the rest to the container.

**Your big world:**
- **Smart splitters:** 212, of which 182 store rules (200 *any*, 185 *overflow*, 159 *none*, 1 *any undefined*, 1 item). Overflow setups, not sorting: the common patterns are *any* / *overflow* / *none* over the three outputs.
- **Programmable splitters:** none.
- **Priority mergers:** 104, each storing `mInputPriorities` as three numbers:

| Priorities | Mergers |
|---|---|
| `[2, 0, 0]` | 39 |
| `[0, 2, 0]` | 36 |
| `[0, 0, 0]` | 19 |
| `[0, 0, 2]` | 7 |
| `[2, 2, 0]` | 2 |
| `[0, 2, 2]` | 1 |

## What was built

```
World.Domain     Connection(From, To, FromPort, ToPort) · SortRule(Output, Kind, Item) · Logistics.SortRules
World.SaveFile   GameClasses.PortIndexOf (Output1 → 0, Input1 → 0, splitters and mergers only), SortRuleOf
                 SaveFileReader.SortRulesOf (mSortRules); ConnectionBuilder sets the ports
World.Domain.Flow
                 FlowSplitter.Rules · FlowNetwork.Ports
                 SteadyState.SendSorted: item / any / any undefined first, overflow after, none never
                 Damp: after 50 sweeps without settling, accept moves halfway per sweep
World            SnapshotDocument version 4: sort rules, connection ports; version 3 still read
Tests            FlowText + Fixtures/big-world-part.flow.gz: 15,328 buildables of your world
```

## How it works

See `Flow/README.md`, sections 2d (smart splitters) and "Settling" (damping). In short:
- **A smart splitter** divides an item evenly over the outputs whose rules take it. What those can't take goes to the overflow outputs, and *none* gets nothing.
- **In the backward pass**, it passes on only what its receiving outputs accept.

## Your big world

| | Step 14 | Smart-splitter rules | + damping |
|---|---|---|---|
| At 100% / starved / output blocked | 867 / 854 / 703 | 952 / 802 / 670 | 951 / 802 / 671 |
| Not settled | 1 | 2 | 1 (313, the iron-wire part) |
| Simulation | 7 s | 118 s | **11 s** |

The rules let 85 more machines run at full speed. They also brought back a flip-flop in the 15,328-buildable part: a motor line and a cooling-system line feed manifolds of manufacturers that take both, and they swapped 8.988/min every sweep.

**Finding it:**
- I exported the part (a test-only text format, `FlowText`) and cut neighbourhoods around the flip-flop.
- **Every neighbourhood up to 7,880 nodes settles on its own**: only the whole part flips. So there's no small test to build.

**Damping** (`Damp`): after 50 sweeps without settling, what each connection accepts moves halfway towards its new value. I'd tried it in step 14 and dropped it: then it only hid the real bugs. With those fixed, it's what remains. Parts that settle within 50 sweeps (almost all) are untouched.

**The regression test** is that exported part itself, compressed (224 KB):
- with damping, it settles after 71 sweeps;
- without, it fails after 2 minutes at the 2,000-sweep limit.

## Priority mergers and fluids

**Your new test world** replaced the iron-plate merger with a priority merger, and added two fluid truck stations, a fluid platform holding water, a fluid buffer, a second water extractor and a pump.

**Which number is highest, and which input is which.** The merger stores `[2, 0, 0]`. `Input1` takes smelter B's belt, `Input3` smelter A's, and `Input2` is empty. The positions decide it:
- the output belt starts 100 cm on one side of the merger;
- smelter B's belt comes in straight from the opposite side, so it's the **center input**;
- smelter A's comes in from the side.

You gave the center input the highest priority, and it's `Input1` with 2. So **2 is highest**, 0 the default, and priority index *i* is `Input`*i+1*, as for splitter outputs.

**Fluids:**

| | In the save | Read as |
|---|---|---|
| Fluid truck station | `inventory`, in **litres** (680,370 water), `FuelInventory` (rocket fuel); ports `PipeInput0/1`, `PipeOutput0/1`, and a fuel belt port `FuelInput0` | a station; stock ÷ 1,000 to m³ |
| Fluid freight platform | `inventory`, in litres (198,000 water) | stock ÷ 1,000 |
| Fluid buffer | `mFluidBox` (403.997, in m³) and the `mPipeNetworkID` of its ends | that much of the pipe network's fluid |
| Pipe network | `FGPipeNetwork`: `mPipeNetworkID` and `mFluidDescriptor` | which fluid each network holds |

The 36 fluid platforms of your big world were empty when it was saved, so step 14 found nothing there.

**The bug it showed.** Step 14 worked out whether a station unloads for *every* building, before checking its role. A building the adapter doesn't know (here the fluid truck station, with its `FuelInput0`) then broke the import, instead of being reported as unsupported. The direction is now only worked out for stations.

**Your big world:**

| | Smart splitters + damping | + priority mergers and fluid stock |
|---|---|---|
| Simulation | 11 s | 8.6 s |
| At 100% / starved / output blocked | 951 / 802 / 671 | 931 / 822 / 671 |
| Sources of unknown rate confirmed | – | 14 of 50 |

85 of your 104 priority mergers give an input priority. 20 machines went from 100% to starved. That's plausible: a lower-priority line now only gets what's left. But **I haven't checked those 20 one by one.** That's the kind of question the findings API (part 3) should make easy to answer.

The new test world moved the stocks a little: the game ran while you built. The tests follow the new fixture.

## Tests (one behaviour each)

| Where | Tests |
|---|---|
| `SteadyStateTests` | A priority merger takes from its highest-priority input first · tops up from a lower one · equal priorities share evenly · an overflow output gets only what the others can't take · an output set to none gets nothing · two outputs set to any share evenly · an item rule sorts a mixed belt · any undefined takes the items no other output names · the largest part of your big world settles |
| `SaveFileReaderTests` | A priority merger keeps its priority per input · a plain merger has none · a fluid truck station that unloads holds its fluid in m³ and burns its fuel · one that loads is a consumer that burns its fuel · it's fed fuel over its fuel belt · a fluid buffer holds the fluid of its pipe network · the smart splitter keeps its rules per output · a plain splitter has none · a belt leaving a smart splitter knows its output · a belt entering a merger knows its input |
| `SnapshotDocumentTests`, `TestWorldFlowTests` | Version 4 · the rules and priorities survive the snapshot · constructor C's plates go to the container, and nothing overflows to the sink |

**The tests can fail:**
- the four rule tests were red before `SendSorted`, the two priority tests before `Share` knew priorities;
- without damping, the big-world part test fails.

## .NET points worth noticing (all now in the primer)

| .NET | Java/Spring (TypeScript) | Note |
|---|---|---|
| Local functions `bool Takes(int k, ItemId item) => …` | A lambda in a local variable | Declared in a method, using its variables. |
| `GZipStream` | `GZIPInputStream` | A 4 MB text fixture in 224 KB. |
| Extension methods don't bind on `dynamic` | – | In the spike, `((dynamic)x).Value.PathName.Split('.').Reverse()` failed at runtime: cast to `string` first. |

## Choices worth a second look

1. **A smart splitter without saved rules** works like a plain splitter (30 of yours). The save leaves out defaults, and I haven't checked what a fresh smart splitter's default is.
2. **Groups of items** going to different outputs are served one after the other, not interleaved. That only matters for a splitter sorting several items onto shared outputs, which yours don't.
3. **Committing the fixture:** `big-world-part.flow.gz` is derived from your big save: buildable ids, recipes' rates, stock. It's your repository, but say if you'd rather not have it in there.
4. **Damping starts at 50 sweeps** and halves the step. Both are judgement calls that worked on your world; the iron-wire part still doesn't settle, as before.

## Next

- **Part 3: findings over the API.** Per world snapshot: machines below 100% and why, bottlenecks, unused supply, sources of unknown rate. Each with where to find it in the game.
