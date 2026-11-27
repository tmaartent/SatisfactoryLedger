# Step 115 · Line-following reads the analysis

**Goal:** issue #51, the first ticket of card 3 (#50, a factory's handovers). Following a line upstream or downstream reads the analysis through `FlowReadings`, the one reader since step 113, instead of a `Func<BuildableId, double>` that every caller filled the same way. A prefactor for the handovers module; no behaviour changes. Branch `ticket/51-line-following-reads`, from `refactor/factory-handovers`.

## Design

- **Module:** line-following in `World.Domain`: `InputSources.Of` (upstream), and `OutputDestinations.Of` and `ReceiversGet` (downstream). It sits on the seam between the graph walk and the stored analysis, which `FlowReadings` owns.
- **Interface change:** the last parameter of those three entry points is now `FlowReadings readings` instead of a callback.
  - `InputSources.Of` weighs each branch by `readings.OutOf(from, item)`.
  - `OutputDestinations.Of` shares at a split by `readings.Into(next, item)`.
  - `ReceiversGet` sums `readings.Into(receiver, item)`.
  - Before, all four production callers (`FactoryInputs`, `FactoryOutputs` twice, `BlockageTrace`, `ShortageTrace`) passed exactly those lambdas. The interface asked for a choice that nobody made differently, so the reading now lives with the walk.
- **Not changed:** `InputSources.CouldBringMore` keeps its four callbacks. Its rules (stations' 30 minutes of stock, sinks giving way) live in the caller's lambdas and move into `FactoryHandovers` in #52, where it becomes private (decision 11).
- **Deletion test:** nothing new was added, and two test helpers were deleted (`Sending` and `Flowing`). Deleting the `FlowReadings` parameter would bring back five copies of the same lambda.
- **Seams under test** (decision 12 in #50):
  - **C:** the line-following tests, which now build their readings with `TestReadings`. They catch the walk (splits, merges, loops, ends) read through the real reader. They miss how the factory findings group the result.
  - **B, the safety net:** the 23 `ForFactory` tests and the golden fixtures, unchanged. They catch any change in what the findings report.

### Choices to overrule

- **`world` stays a parameter** next to `readings`, though `FlowReadings` holds a world too. That world is private to the reader, and the walk needs the graph itself (`Upstream`, `Downstream`, `Find`). Exposing it from the reader would widen `FlowReadings` for one caller. Decision 13's `FactoryHandovers.Of(world, readings, …)` has the same shape.
- **`ReceiversGet` compares with a precision of 4** (`Assert.Equal(24, …, 4)`). The assembler now takes `0.8 × 30` through the reader, not a literal 24, so the last bit of the double may differ.
- **Submodule in the worktree:** a fresh git worktree has no `external/SatisfactorySaveNet`, and `SaveFile` doesn't build without it. I ran `git submodule update --init` there. This doesn't touch the repository itself.

## How the tests changed

The expected values stayed the same. Only the analysis they set up changed: a hand-written callback became readings of the small world.

| Before | Now |
|---|---|
| `Sending(("from A", 30), ("from B", 10), …)`: what a buildable sends on | `new TestReadings().Into("from A", IronIngot, 30).Into("from B", IronIngot, 10)….Build(world)`: what flows into each belt, from which `OutOf` reads what it sends on |
| `Flowing(("left", 20), ("right", 10))` | `.Into("left", IronPlate, 20).Into("right", IronPlate, 10)` |
| `ReceiversGet`: `{ assembler = 24, sink = 6 }` by hand | `.Starved("assembler", IronPlate, 0.8)`, so it takes 0.8 × 30 = 24, and `.Into("rest", IronPlate, 6)` for the belt to the sink |

The merge test (`Where_other_supply_merges_in_…`) now also reads what the merger carries through `FlowReadings.Carrying`: the sum of the belts that feed it, which the lambda used to give as a literal.

## C# points

| Here | In Java / TypeScript |
|---|---|
| **A callback parameter replaced by the object that answers it.** `Func<BuildableId, double>` is a delegate type, which each caller filled with a lambda over `readings`. Passing `FlowReadings` lets the method choose which reading it needs (`OutOf` or `Into`), and the item it reads for. | Swapping a `Function<BuildableId, Double>` for the service it came from. The same trade-off: a narrower type for the parameter, but a choice the caller never made differently. |
| **`onwards.Sum(next => readings.Into(next, item))`** replaces `onwards.Sum(flowInto)`. The old version passed a delegate as a method group; the new one needs a lambda because `Into` takes two arguments. | `onwards.stream().mapToDouble(next -> readings.into(next, item)).sum()`; the old one was `mapToDouble(flowInto::apply)`. |
| **`using SatisfactoryLedger.World.Domain.Tracing;` in `World.Domain`**: the parent namespace now reads a type from its child namespace, and the child already read the parent. That's allowed within one assembly. Only project references can't form a cycle. | The same as Java packages within one module: packages may depend on each other in a cycle; only JPMS modules or Maven modules can't. |

No new primer entry. The `Assert.Equal(expected, actual, precision)` overload is already under Testing in `primer.md`.

## Tests

- Red first, one entry point at a time. The rewritten `InputSourcesTests` didn't compile (argument 5: `FlowReadings` to `Func<BuildableId, double>`). Then `InputSources.Of` changed, and they passed. After that, the same for `OutputDestinationsTests` and the two downstream entry points.
- `dotnet build SatisfactoryLedger.slnx`: 0 errors, exit code 0.
- `dotnet test SatisfactoryLedger.slnx`: 1,298 passed, exit code 0. That's the same count as step 114: tests were rewritten, none added.
- The golden fixtures and the `FactoryInputsTests`, `FactoryOutputsTests` and other tests in `World.Tests` didn't change.

## Files

- **Domain:** `InputSources.cs`, `OutputDestinations.cs`, `Tracing/BlockageTrace.cs`, `Tracing/ShortageTrace.cs`
- **Module:** `Analysis/FactoryInputs.cs`, `Analysis/FactoryOutputs.cs`
- **Tests:** `World.Domain.Tests/InputSourcesTests.cs`, `OutputDestinationsTests.cs`
- **Docs:** this log
