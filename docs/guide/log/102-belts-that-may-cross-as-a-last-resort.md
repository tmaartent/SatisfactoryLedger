# Step 102 · Belts that may cross as a last resort

**Goal:** some runs still left belts unrouted after step 101. On the owner's Desert Start Cleaned world (Fast, supply lines, 32 m ceiling) 2 were left over with only 7 of 16 levels used, so crossings stopped them, not height (2026-11-25). Branch `feat/arrangement-touch`, after steps 100 and 101 were committed.

## The owner's choices (2026-11-25)

| | before | after step 102 |
|---|---|---|
| straight over straight | ✓ | ✓ |
| crossing where one turns | ✗ | ✓ last resort |
| parting from a stack on their own sides | ✓ | ✓ |
| parting swapped | ✗ | ✓ last resort |
| under a lift, on a level it leaves free | ✗ | ✓ always |

- **Clean layout first:** each layout is routed without such crossings; only the belts left over get them. Layouts compare by fewest unrouted, then fewest last-resort crossings, then least belt work, so a clean layout always wins.
- **Passing a lift is always allowed, and isn't a last resort.** A lift under a factory runs from its belt's level up; one at a spot runs from its belt's level down to the ground.
- **Shown:** marked on the map and in 3D, and counted in the run's summary ("2 crossings").

## What was built

- **Lifts on their own levels** (`BeltRouter.cs`):
  - `End` got a `Lift` of the new `file enum LiftSpan`:
    - `Up` for a factory's squares;
    - `Down` for a spot on the floor;
    - `Whole` for the floor's edge, an existing belt's entry and a joint on another belt.
  - `Board.Lay` takes the two ends, and marks only the lift's levels as taken on its first and last squares.
  - `Board.Crossings` no longer refuses a belt that shows once on a square (a lift). The levels taken keep the passing belt off the lift's own levels.
  - A new lift still only goes on a square with nothing on it (`Free`), so a lift never lands on belts passing below.
- **Last-resort crossings:**
  - `Crossings(…, lastResort)` returns `(Crossed, Forced)?`, where `null` means "may not go so". With `lastResort`, an interleave that isn't straight over straight counts as forced.
  - `Search` adds `LastResortCross = 1 << 50` per forced crossing: more than any length of belt, so a belt takes the way with the fewest.
  - `Board.Find(…, lastResort)` walks the states it found, before laying them, and returns the squares crossed.
  - `BeltRouter.Lay`: each try lays every job cleanly (`LayAll(order, lastResort: false)`), then the unrouted ones again on the same board with `lastResort: true`.
  - Tries compare by (left over, last-resort crossings, fallbacks), and go on while any is above 0. A job that needed crossings counts as failed, so the next try lays it earlier.
- **Through the layers** (named `LastResortCrossings` everywhere):
  - `RoutedBelt` and `BeltRouting.LastResortCrossings` (the sum);
  - `RoutedBeltDocument` (nullable, so stored runs from before still read);
  - `RoutedBeltResponse` (world points, via `grid.WorldOf`);
  - `ArrangementSummary` (`int?`: null for runs from before);
  - `api/openapi.json`.
- **Placing again** (`ArrangementDocuments.Solve`): layouts compare by (unrouted, crossings, cost), and placing goes on while either count is above 0.
- **Web** (`arrangement-model.ts`):
  - `crossingMarks` gives each crossing with the belt's height and level there, and a tooltip;
  - `runSummary` adds "· 2 crossings" / "· 1 crossing";
  - the map draws an amber × (`.crossing-mark` in `styles.scss`), and the 3D view an amber ball, faded and hidden with the belt's level.
- **Docs:** BR-O2, the ADR-0028 amendment, the glossary (*Last-resort crossing*, *Lift*), and the primer.

### Choices to overrule (mine, not the owner's)

- **No last-resort crossing between a belt and a pipe.** An existing test caught this: in the last-resort pass a pipe "crossed" a belt by swapping places with it along a shared row. That is a pipe and a belt running parallel on different levels, a mixed stack, which BR-O2 forbids. A pipe and a belt with four distinct sides that interleave are always straight over straight anyway, so nothing is lost.
- **Never between two running straight the same way.** Along its run a stack keeps its order; it may only part swapped where the belts join or leave it. The swap test checks the crossing is at one of those two squares.
- **One mark per square,** even if a belt crosses two belts there.
- **Passing under a factory's lift is rare.** A stack builds from the floor up, so a factory's belt usually leaves at level 0 and its lift takes every level. Passing **above** a spot's lift is the common case.
- **No test of "fewer crossings though longer" within one belt.** It follows from the weight (`1 << 50` against at most about `1 << 47` of belt), and a floor that shows it took more lifts than it was worth. There is a test that a clean way goes before a shorter one that would cross.

## On the owner's Desert world

The same settings again (snapshot `01a20f51…`), run `01a212f4-b872-7a79-a247-111685b1040d`, **added to that world's runs** (delete it from the run list if you don't want it):

| | unrouted | crossings | placements | levels | time |
|---|---|---|---|---|---|
| step 100 | 3 | – | 1 | 7 | |
| step 101 | 2 | – | 3 | 7 | 88 s |
| **step 102** | **1** | **0** | 3 | 8 | 108 s |

No crossing was needed: passing over spots' lifts gave the room. The belt left is from a Mk.3 miner north of the area. It must leave the floor where its existing belt comes in on the north edge, and the squares there are most likely taken by belts laid earlier: an end needs a square with nothing on it yet. Crossings can't help there. It is a separate limit, for the owner to decide on.

## .NET points

- **A positional pattern on a nullable tuple:** `Crossings(...) is not (int crossed, int forced)` checks for `null` and unpacks in one test. `is not var (…)` would work too, but reads as "always matches". Java's record patterns need a record. See `primer.md`, *.NET basics*, "A positional pattern on a nullable tuple".
- **`ValueTuple.CompareTo` as a lexicographic order:** `(left, crossings, fallbacks).CompareTo(…) < 0` replaces Java's `Comparator.comparing(…).thenComparing(…)`. See `primer.md`, "Comparing value tuples".
- **A `file enum`:** `LiftSpan` is visible only in `BeltRouter.cs`, like `Board`, `State` and `End`: the one exception to one type per file (SA1402). Java's nearest is a package-private top-level type, which the whole package still sees.
- **A local function capturing the loop's state:** `LayAll` inside the `for` over tries sees `board`, `laid`, `unrouted` and the rest, like a Java lambda, but it may change them. A Java lambda only captures effectively final locals, so you'd need a holder object or a class.
- **Required positional parameter before optional ones:** `RoutedBelt.LastResortCrossings` went before `StartsOnBelt = false`, since C# requires optional parameters last. The document keeps it optional and nullable (`= null`), so `System.Text.Json` reads old stored runs.

## Tests

- **Router** (`BeltRouterTests`, 38). Each new test failed before the code:
  - a belt passes **under** a factory's lift below the level its belt leaves at (it went round, 2 squares longer);
  - a belt passes **over** a spot's lift;
  - a straight belt crosses another where that one turns, as a last resort, once, on another level, at one of that belt's corners;
  - a turning belt crosses a straight one, the same;
  - two belts in a stack part swapped as a last resort, where they join or leave the stack, on another level.
- **Router guards**, which passed before and after:
  - a last-resort crossing needs a level of its own: on one level the belt is reported;
  - a clean way, twice as long, goes before a shorter one that crosses;
  - clean routings have no last-resort crossings.
- **Router, renamed:** "A belt never passes through a lift" is now "…never passes a lift on the levels it takes".
- **Documents:** with belts between spots, which every layout routes the same, a layout needing a crossing is placed 3 times and keeps 1 crossing. A layout that routes cleanly is placed once and has none. Both failed first.
- **API:** a clean run says 0 last-resort crossings, and each belt has none.
- **Web:** `crossingMarks` (place, height, level, tooltip, none when clean) and the summary line. My first red check ran bare `vitest`, which can't load the Angular specs, so it failed on loading rather than on the assertions.
- **Results:**
  - 1,243 .NET tests pass, with no build warnings;
  - 426 web tests pass, exit code 0; so do lint, the format check, the build and the e2e type check;
  - the Playwright recording still fits the changed API.
