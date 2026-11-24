# Step 89 · A run says the levels it uses, not its tallest stack

**Goal:** a run's summary said "stacks up to 5" while its 3D view showed 8 levels (owner, 2026-11-24). Branch `feat/ctrl-drag-arrangement`, uncommitted, after step 88 (PR #24).

## Why the two differed

Both were right, but they counted different things. The **tallest stack** (`Board.TallestStack`) was the most belts on one square. The **levels** in 3D are the heights belts run at. A belt keeps its level along a straight run, and that level must be free on every square of it, so it may run high over a square with few belts. For example, a run crosses square A with levels 0–2 taken and square B with 3–6 taken: it goes on level 7, and neither square holds more than 5 belts.

The owner chose to report the levels used: they are the floor's height, what the ceiling limits and what's built.

## How it's built

- **`BeltFloor.LevelsUsed`** (World.Domain): given each belt's levels, the highest plus one; 0 without belts.
- **`ArrangementSummary.LevelsUsed`** (World) replaces `TallestStack`: worked out from the run's stored belts, so runs made before say it too; null without routed belts, or with belts from before levels were kept.
- **The page** says "8 levels (16 m)" after the belt metres.
- **`TallestStack` is gone** from `Board`, `BeltRouting` and `ArrangementProposalDocument`: nothing would use it (no test-only production code). System.Text.Json skips unknown fields by default, so stored runs that still carry `tallestStack` read fine, like Jackson with `FAIL_ON_UNKNOWN_PROPERTIES` off.

ADR-0027 amended; the glossary's *Stack* says it.

### Technical choices to overrule

- **2 m a level** is written in the page's template (as in `BeltFloor.LevelMetres`), not sent by the server.

## Tests

- Domain: the levels a floor uses reach its highest belt, not its tallest stack (5 at most on a square, one belt on level 7: 8 levels); a floor without belts uses none. The router tests that checked stacking through `TallestStack` check it through the levels the routed belts use.
- API: the summary's levels used are those of the belts it answers with.
- All .NET tests pass (1181, exit code 0). Web: 338 pass, lint and format check pass, build clean.
