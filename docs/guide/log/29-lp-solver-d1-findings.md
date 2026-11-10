# Step 29d1 · The LP solver, part D1: findings, parts, and the sweep's own tests

**Goal:** a full `FlowResult` from the linear programmes: flows, machine states with their causes, bottlenecks, unused and unknown supply, solved part by part. Then the sweep's `SteadyStateTests` run against the LP as the parity check. Not wired in yet. Branch `feat/lp-solver`.

## Parity

`SteadyStateTests.cs` is now linked into `World.Simulation.Tests` too. There, a compile symbol (`LINEAR_STEADY_STATE`) makes `TestFlow.Run` use `LinearSteadyState.Of` instead of the sweeps.

**66 of its 71 tests pass against the LP.** That includes every finding: starved of, blocked on with shares, bottlenecks, unused supply, stock, stations, drone ports, generators, loops.

The other 5 are about **how sweeps settle** (sweep counts, approximate parts, the sweep's own run on the big world). They're fenced with `#if !LINEAR_STEADY_STATE` and go when the sweep goes.

## How the findings are read

The solution gives flows and utilisation. "Why" needs **what-ifs**: a few bounds relaxed, re-solved from the current solution, then restored.

| Finding | What-if |
|---|---|
| Offered share of an input (BR-A11) | The machine may destroy its products and has its other inputs unlimited, and its input shares may leave their level. Maximise `u`. |
| Room for its products, per form | Its inputs are unlimited, and its products' shares may leave their level. Maximise `u`. |
| Bottleneck: what's offered on top (BR-A12) | The belt has no capacity and may swallow what comes in (`in ≥ out`). Maximise what flows into it. |

Starved or blocked follows the sweep's rule: blocked when the outputs take less than the inputs offer, otherwise starved of the scarcest input.

**Speed, on the 15,328-buildable part of your big world** (Debug build):

| | Time |
|---|---|
| First version | 93 s |
| One programme kept across rounds (only bounds change) | 90 s: the 42 rounds take 4.5 s, so they weren't the cost |
| **What-ifs spread over copies, one per thread** | **28 s** |

The what-ifs were the cost: about 900 solves for 291 machines below 99.9%. Each answer is an optimal *value*, the same on any copy of the programme. So a part with many held machines builds one copy per thread with the same capped shares, settles it, and answers a slice. An OR-Tools solver isn't for sharing between threads. The sweep does the same part in a few seconds.

## Files

| Where | What |
|---|---|
| `World.Domain/Flow/Objects/FlowNetwork.cs`, `NetworkPart.cs` | `Parts()`: the union-find, now shared by both solvers |
| `World.Simulation/LinearSteadyState.cs` | `Of(network)` → `FlowResult`, part by part; held machines on copies in parallel |
| `World.Simulation/LinearProgramme.cs` | `Cap` (bounds in place), `Settle`, the what-ifs (`OfferedShare`, `OutputRoom`, `OfferedInto`) |
| `World.Simulation/LinearFindings.cs` | Machine states, bottlenecks, unused and unknown supply |
| `tests/…World.Simulation.Tests/` | The LP tests on `FlowResult`; `SteadyStateTests.cs`, `FlowResultQueries.cs`, `FlowText.cs` linked in; `BigWorldTests` |
| `World.Domain.Tests/TestFlow.cs`, `SteadyStateTests.cs` | `#if LINEAR_STEADY_STATE` |

## Points worth noticing

| .NET / C# | Java | Here |
|---|---|---|
| `#if SYMBOL` with `<DefineConstants>` in one project: the same linked source compiles differently there | No preprocessor; a system property or a Gradle source set | `TestFlow.Run`, the 5 sweep-only tests |
| `Parallel.For` with one native solver per iteration | `IntStream.range(…).parallel()` or an `ExecutorService` | `LinearSteadyState.HeldMachines` |
| A positional record deconstructs in `foreach ((FlowNetwork part, IReadOnlyList<int> edges) in network.Parts())` | Records with pattern matching (Java 21) | `SteadyState.Of`, `LinearSteadyState.Of` |
| `?? []` can't follow a `ValueCollection`: a collection expression has no type of its own to fall back to | — | `(dictionary ?? []).Values` instead |

## Tests

19 new runs of the sweep's cases, plus `BigWorldTests` (the largest part solves, and settles). Red first: 6 of the sweep's 71 failed. One was a real gap: a chain of three 60/min belts showed no bottleneck, because the what-if left the second belt in charge. Fixed by letting the belt in question swallow its inflow. **Totals:** 869 .NET tests.

## Choices worth a second look

1. **`Settled` is always true and `Sweeps` is the number of rounds.** The approximate parts of BR-A10 don't exist for the LP. When wiring it in (D2), BR-A10 and the "approximate" wording in the findings need your decision.
2. **28 s against a few seconds** for the big part. It's a background analysis, but there's more to gain: a Release build; and fewer what-ifs, for instance skipping the offered share of an input that already arrives in full.
3. **Results differ slightly between runs of the rounds** where the LP is degenerate: 287 or 291 held machines, depending on the order of capping. Exact answers are optimal values; which equally good corner GLOP picks isn't fixed.

## Next (D2)

Clogging, sources of unknown rate sending in proportion, a machine's products evenly over several outputs. Then compare LP and sweep on your whole big world, wire the LP into the analysis service (a new analysis version), delete the sweep, and move `SteadyStateTests` over.
