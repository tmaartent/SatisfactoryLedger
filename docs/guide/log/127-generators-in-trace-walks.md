# Step 127 · Generators in the trace walks, and one answer for what a buildable makes and accepts

Card B of the architecture review of 2026-11-28 (`docs/architecture/reviews/2026-11-28.md`), spec #73 with tickets #74, #75 and #76. The first step run with `/implement-spec`: an implementer agent per ticket, each in its own worktree, the two fixes in parallel and the refactor after them.

## Design

- **Domain rule (owner, 2026-11-28, without a grilling, one question):** a generator burning a known fuel counts as a machine in both trace walks. It accepts only what it burns, and it's a source of its waste only when some leaves it. A sink, an uploader, a station that loads and a generator of unknown fuel accept every item, as before. Now in BR-A20 and BR-A21.
- **Module:** `Buildable` owns "what it makes, what it accepts, what it holds". Its interface grows:
  - `Makes(item)`: a machine's recipe products, a generator's waste when some leaves it;
  - `Accepts(item)`, virtual: what it consumes; `Consumer` overrides it to "everything, unless it burns a known fuel";
  - `DemandOf(item)` and `ProductionOf(item)`: its rates at 100%, zero when none;
  - `InputStock`, moved up from `Machine` and `Consumer`.
- **`Consumer.BurnsKnownFuel`** names what `Consumer { Generation: not null }` spelled out in several places: a generator whose rates are known, so the flow and the readings count it as a machine. `IsGenerator` stays the role (BR-W2).
- **Deletion test:** delete the new members and the type switches come back in the two walks, the readings, the flow and the stock check; the handovers' copies of "would make more" and net demand come back too. They earn their keep.

## How it was built

1. **#75 (shortage walk), red first:** `A_generator_that_leaves_none_of_its_waste_is_not_where_the_waste_comes_from` expected *nothing brings it* and got *too few producers*. The walk read `Generation.Byproduct` instead of the buildable's `Produces`, which holds the waste only when some leaves. A second test, that a generator leaving waste is still its source, was green from the start: it guards what must stay.
2. **#74 (blockage walk), red first:** `A_generator_that_does_not_burn_the_item_is_nowhere_for_it_to_go` (iron plates into a coal generator) expected *nowhere to go* and got *too few consumers*: the bare `case Consumer:` took any item.
3. **#76 (refactor), no behaviour change:** the walks, `FlowReadings`, `WorldFlow`, `SnapshotAnalysis.OnStock` and `FactoryHandovers` ask the buildable. `cannotLeave` goes through `FlowReadings.MakesMore`; net demand at 100% is written once; the private "at 100%" wrappers go. New tests in `BuildableRatesTests`, red first (members missing, then the take-everything rule).

The two fixes ran in parallel, merged by fast-forward and a cherry-pick so the history stays linear; #76 started once both tickets were closed (GitHub dependencies).

**Test runs:** .NET 1346 passed; the test worlds' stored analyses unchanged (World.Simulation 62).

## Review

Two axes against `main`. Nothing implemented wrongly; the refactor was checked switch arm by switch arm.

- **Hard (Standards):** BR-A20 and BR-A21 didn't say what the code now does. Fixed.
- **`Takes` clashed with the glossary,** where *Takes* is a rate: the yes/no question is `Accepts`.
- **The stock test checked two behaviours:** split.
- **`OnStock` still had its own empty-stock fallback:** gone.
- **Left, with reasons:**
  - "A machine without a recipe" isn't one question. The blockage walk asks `Consumes.Count == 0`, the shortage walk `Produces.Count == 0`; a recipe with inputs and no products (the test worlds have them) tells them apart.
  - `AtFullSpeed` keeps its two delegates. Writing it as net demand negated would round differently when a machine has the item on both sides; the stored analyses would change.
  - `Accepts` stays on every buildable (Refused Bequest, judgement call): moving it to `Machine` and `Consumer` brings the type switch back to the walk.

## .NET points

| Here | In Java |
|---|---|
| **A `virtual` member with an `override`** (`Buildable.Accepts`, `Consumer.Accepts`): C# methods are non-virtual unless they say so. | Every Java method is virtual unless `final`; C# is the other way round. |
| **`case (Machine or Consumer) and { } taker when taker.Accepts(item)`**: a type pattern, a property pattern binding a name, and a guard, in one `case`. | Java 21's `case Machine m when …`, without `or` across types. |
| **Same maths, different double.** `a + b - c` and `a - c + b` can differ in the last bit, so a "no behaviour change" refactor keeps the order of additions. The stored analyses are what catches it. | The same in Java: IEEE 754 doubles, the same rounding. |

## Choices to overrule

- **`DemandOf` and `ProductionOf`** are on `Buildable`, beyond the three questions the spec named: they replace the walks' private "at 100%" wrappers.
- **The two review items left as they are** (above), for the reasons given.
- **The domain rule was settled in one question**, not a grilling, as asked.

**Files:** `World.Domain/Buildable.cs`, `Consumer.cs`, `Machine.cs`, `FactoryHandovers.cs`, `Flow/WorldFlow.cs`, `Tracing/BlockageTrace.cs`, `ShortageTrace.cs`, `FlowReadings.cs`; `World/Analysis/SnapshotAnalysis.cs`; `tests/SatisfactoryLedger.World.Domain.Tests/BuildableRatesTests.cs`, `Tracing/BlockageTraceTests.cs`, `ShortageTraceTests.cs`; `docs/domain/business-rules.md`; `docs/architecture/reviews/2026-11-28.md`; this log.
