# Step 48 · Belts on the floor below

**Goal:** route the belts between the arranged factories on the proposal, laid out like a circuit board: at right angles, never crossing (asked 2026-11-18). Branch: `feat/optimal-arrangement`, on top of step 47 (committed first), uncommitted.

## What you decided (2026-11-18)

- Belts at **right angles**, **never crossing seen from above**.
- They run on a **floor of their own below the factories**, free everywhere on it, under a **configurable ceiling**: 8 foundation heights of 4 metres (32 m) by default.
- Belts may **stack** (stackable poles, **2 m** apart: 16 under 32 m), and **a stack keeps its order**: two belts that share a stretch may not leave it swapped.
- **Lifts** take a belt up into its factory; in this first phase they're the only thing in the way. Lifts down to a transport layer and storage containers come later.
- **Route after placing**: the placement doesn't change.
- **One belt per link** (each of a link's belts on its own), no shared bus.

All of it is BR-O2; the algorithm is ADR-0028 (*Proposed*: yours to accept or change).

## What I chose (overrule any)

| Choice | Why |
|---|---|
| **Squares of one foundation (8 m)**, one stack per square | Your plots and roads are whole foundations, so factories line up with squares exactly. |
| **A lift anywhere under its factory**, on a free square of its own | You said lifts block; where in the factory a lift lands is yours to build. |
| **Shortest first, then fewest turns** | Tidy, circuit-board-like L-shapes. A turn only matters when two ways are equally long. |
| **Belts beside each other where there's room, stacked only where there isn't** | I first wanted belts to prefer joining a stack, but it isn't something you asked for, and running side by side is what a circuit board does. |
| **Links routed one at a time, shortest first; a belt that can't get through is reported** | Simple and deterministic. Rip-up-and-reroute could rescue some reported belts on crowded floors; not needed yet. |
| **Only links between arranged factories** | A link with a factory outside the area would leave the floor. |
| **Pipes routed like belts** | Fluid links need belts (pipes) too; stackable pipe supports exist in the game. |

## On your world, in the model

Your last area (48 factories), plots of 6 × 6 foundations with roads of 4, keep together 2, a ceiling of 32 m, 60 s:

| | |
|---|---|
| Belt work (placement) | 282 → 135 (−52%) |
| Belts routed | **67 of 67**, none reported |
| Length together | **6,272 m** (lifts not counted) |
| Tallest stack | **2** belts |
| Routing time | about 0.3 s of the run's 60.3 s |

With this much room, belts nearly always find a way beside each other; stacks only form where lifts leave a gap.

## After your first look (2026-11-18)

You saw the belts, but none to stations, drone ports or nodes, and some blocks were huge.

**Huge blocks.** Dark Matter Crystals, Neural Quantum Processors and five more took 128 × 128 or 128 × 48 m. Not wrong machines: the Particle Accelerator's and Quantum Encoder's footprints (the game's hard clearance; the soft part was already left out) are about 50 m long, just over a 48 m plot, so they took two plots and the 32 m road between them. You decided **every factory takes one plot**: you build them to fit, on several floors where needed. `ArrangementGrid.PlotOf` puts a factory on the plot where its machines' middle is; BR-O1 says so.

**Belts to nodes, stations and drone ports.** You chose: from nodes' extractors, unloading stations and drone ports into the arranged factories, and from them to loading stations and drone ports; ending at a **lift down at the building's spot**; a spot **outside the area** reached at the floor's edge facing it, marked where it leaves the floor. First *only routed*, placement unchanged.

On your area that made it plain why they had been left out of placing: their belts averaged 927 m (up to 3 km), 71 of 233 couldn't get through, stacks hit the 16 under the ceiling. You chose to **weigh them lightly in placing**: a new setting *Nodes & stations weigh*, 0.5 of a belt between factories by default.

| Your area, in the model | Only routed | Weighed at 0.5 |
|---|---|---|
| Belts routed | 162 | **192** |
| Not routed | 71 | **41** |
| Length together | 95 km | **53 km** |
| Belts to nodes and stations, average | 927 m | **307 m** |
| Belts between factories, average | 110 m | 211 m |
| Tallest stack | 16 | 13 |

The 41 left: 17 from freight platforms, 16 to and from drone ports, 6 between factories, 2 from miners. A drone port's few squares limit its lifts, and greedy shortest-first routing walls some in; rip-up and reroute is the obvious next lever (ADR-0028).

How it's built:
- **Spots** (`BeltRouter.Route(…, spots)`): a building's squares on the floor; a belt ends at a free one. For a spot off the floor, the floor's edge squares are the ends, each charged the way still to go; `RoutedBelt.LeavesFloor` marks it.
- **Links weigh in percent** (`ArrangementLink.WeightPercent`): 100 between factories, the setting for spots. The solver and the cost shown both use it, so *now → proposed* compare like for like.
- **From the world** (`WorldArrangement`): the input table's sources that are a `Provider` (miners, extractors, unloading stations) or a station's class; and `FactoryOutputs.Ends`, the line-following the output table already used, now shared, for outputs reaching a station or drone port. A node is a fixed place that **doesn't block** (as in step 47's first runs); a station still does.
- **The page**: belts to spots in the colour of the factory they serve, a dark dot at the spot's lift, an amber ring where a belt leaves the floor; the *Nodes & stations weigh* setting and a column for it.
- Migrations `ArrangementSpotWeight` (0 for older runs: they didn't weigh them).

## Too many belts at Singularity Cells (2026-11-18)

You saw Singularity Cells with very many belts: 15. Four of them carried 2.5 Singularity Cells a minute to **one** drone port. That was a bug: the factory's four Manufacturers each reach the port over their own line, and each line's share was rounded up to a whole belt before adding them together. Now a link's rate is added up per item first and rounded once (`WorldArrangement`, `spotRates`): one belt. Your area again, in the model: 191 belts routed, **19 not** (was 41), 62 km together, tallest stack 16.

No test pins it: the test save has no factory that sends anything to a station (checked with a probe, then removed). It's checked on your world only.

The other eight are real in the model: one belt from each of four miners (iron ore 600 a minute; stone 600, 300 and 300) and one pipe from each of four water extractors (250 a minute each), because each building is its own spot and a link has its own belts (BR-O2: no shared bus yet).

## Extractors merge (2026-11-18)

You said: merge resource providers up to the highest belt speed, limestone 300 + 300 + 600 is one 1200 belt, not three. Now extractors feeding a factory one item share belts (`SupplyBelts.Merge`, World.Domain): a belt starts with the biggest extractor left and takes the nearest ones that still fit; the top belt's capacity for solids, the top pipe's for fluids. A shared belt's spot is the rectangle around its extractors, with its lift anywhere over them. Stations and drone ports still have a belt per building (I chose; you said resource providers).

Your area, in the model: Singularity Cells 12 → **8** belts (iron ore 1, limestone 1, water 2 pipes). All of it: **166 belts, 43 km** (was 191 and 62 km), tallest stack 11 (was 16), 34 not routed: 20 from freight platforms, 5 from miners, 4 between factories, 4 to or from drone ports, 1 from a water extractor. Runs differ: the placer stops at 60 s with a good layout, not the best, and greedy routing follows it, so the previous run's 19 isn't directly comparable.

Tests (`SupplyBeltsTests`, 4): limestone from three miners merges onto one full belt · what a belt can't carry goes on the next · the nearest extractors share a belt · water extractors fill pipes of their capacity. The test save has no factory fed by several extractors of one item, so the merge in `WorldArrangement` is checked on your world only.

## Pipes, stations merging, rip-up and reroute (2026-11-18)

You said: stations and drone ports merge by the same rule; rip up and reroute; and pipes too, straight bends only, never crossing, stacked on stackable pipeline supports. I asked three things and you chose: **a stack is of one kind** (belts or pipes, never both), **2 m a pipe level**, and **a factory's outputs to several stations merge too**.

- **Pipes.** `ArrangementLink` now has `Pipes` beside `Belts` (fluids over the top pipe's capacity); placing counts both alike (`BeltsAndPipes`). The router lays a link's belts, then its pipes, with the same search; the board remembers per square whether its stack is of pipes, and a belt or pipe only passes a square that's empty or holds its own kind. `RoutedBelt.Pipe` goes out to the page, which draws pipes dashed.
- **Stations and drone ports merge.** What feeds a factory one item, extractors, stations and drone ports alike, goes through `SupplyBelts.Merge`. What a factory sends to stations is added up per station first (several lines can reach one), then merged per item the same way: one belt leaves the factory and, in the model, goes down a lift over the stations it serves (in the game it would split at each).
- **Rip-up and reroute** (`BeltRouter.Route`). When belts are left over, every belt is taken up and the whole floor laid again, the links that failed first, the most often failed foremost, then the rest shortest first. At most 8 tries; the try with the fewest left over is kept. *I chose reordering whole tries over the finer kind (take up only the belts in a failed one's way): no change to the search, and nothing to undo when a reroute fails. Overrule if you want the finer kind.*

Your area, in the model (run `01a1eed8`): **123 routed, 9 of them pipes; 7 left over** (was 34), 39 km together, tallest stack 12. The belts needed fell from 200 to 130, mostly from merging the freight platforms. The 7 left over: four miners to Heavy Modular Frames, one miner to Stators, one water pipe to Biochemical Sculptors, one train station to Steel Beams, and one between Superposition Oscillators and AI Expansion Servers. Routing takes about 8 seconds of the run's 70. I can't say how much of the drop is rip-up and how much is merging: the placer stops at 60 s, so each run places differently.

Tests: `BeltRouterTests` +4: a link's pipes are routed as pipes · pipes stack with pipes · **a pipe never joins a stack of belts** · **a belt that can't get through is routed first on another try** (shortest first, one belt runs over the other's only lift square). `ArrangementProblemTests` +1: a link's pipes count as its belts do. `ArrangementSolverTests` +1: pipes draw factories together as belts do. With one try only, the rip-up test fails; without the kind check, the pipe-and-belt test fails; each fails alone. As before, the test save has no stations to merge, so that part is checked on your world only. **Totals:** 959 .NET tests, 99 web tests.

## Levels: the belt floor in 3D (2026-11-18)

You asked whether the belt network could be drawn in 3D, and set the rule for heights: **a belt keeps its level from turn to turn; where it turns it may change level, by a ramp for 2 m, by a lift for 4 m or more.**

- **Domain.** `RoutedBelt.Levels`: its level on each straight run. `RoutedBelt.Lifts`: the corners where it changes 4 m or more. `BeltFloor.HeightOf(level)` (2 m a level, from 0) and `BeltFloor.ClimbNeedsLift(levels)` hold the rule, and the router uses the same rule for its costs: belt work, then turns, then a ramp, then a lift, then low before high.
- **How the router keeps levels.** Each square knows which levels are taken; one belt to a level. The order on each side stays what it was: it's about crossing **seen from above**. My first try used that order as the height. It failed: a belt joining a stack from the south had to go *under* the ones there, and with the first belt on level 0 there was no under. Now the level is kept apart from the order.
- **Fast enough.** Searching every level at every square took **8½ minutes** on your area instead of 70 seconds, mostly for belts with no way at all. So the way is found first with only "a level is free here" asked of each square, then the levels are picked along it: the cheapest over its runs, a small calculation of runs × levels × levels. Only if none fit is it searched level by level. Back to 73 seconds.
- **Web.** `beltPath3d` (`arrangement-model.ts`) turns a belt into points in metres: down its lift from the factory floor at the ceiling, each run at its height, a ramp over the last 4 m before a turn, straight up or down on a lift, up its lift at the end. `BeltFloor3d` (new, `belt-floor-3d.ts`) draws the floor, the factories as slabs on their plots at the ceiling, and every belt and pipe as a tube in its factory's colour (pipes thicker and see-through); drag to turn, scroll to zoom. The page has a toggle *The belt floor in 3D, below the map*.

Your area, in the model (run `01a1ef04`): **126 routed, 8 of them pipes; 4 left over**, 43 km, tallest stack 8. Levels 0 to 10 are used, and **no belt needed a ramp or a lift**: each starts at a level that's free all along its way. I checked the run with a script: no two belts on one level of a square.

Tests: `BeltFloorTests` +2 (levels 2 m apart from the floor up · a belt ramps 2 m, 4 or more needs a lift) · `RoutedBeltTests` (new) +1 (its lifts are where it turns 4 m or more) · `BeltRouterTests` +2 (belts stacked on a square are on levels of their own · a belt keeps its level from turn to turn and climbs only when it must) · `ArrangementDocumentsTests` +1 (routed belts carry their level on each run) · web +4 (`beltPath3d`: lifts at the ends; a ramp over the last 4 m; a lift straight up or down; runs from before levels on the floor). Breaking "a level is taken" fails the stack test, both in the search and in picking levels. **Not pinned by a test:** that a climb may pass only through free levels. I couldn't build a small floor that forces a belt to climb past another, so it's checked on your world only (no clash in the script). **Totals:** 965 .NET tests, 103 web tests. **The 3D view hasn't been tried in a browser.**

**Fixed after your first look:** the tubes had jagged corners and diagonals. `TubeGeometry` samples its path evenly along the whole length, so with a 30 m lift between 800 m runs it cut corners. Each straight stretch is now a piece of its own (a flat box for a belt, a cylinder for a pipe) with a joint at each corner, merged into one mesh per colour.

*I chose (overrule any):* level 0 is on the floor and a stack builds from the floor up; a ramp is 4 m long (half a foundation); a lift down from a factory or a spot goes to the ceiling; pipes drawn thicker and see-through, in their factory's colour.

## What a belt carries (2026-11-18)

You asked to see on the lines on the map what they carry. Pointing at a belt or pipe now shows its item and rate, e.g. *Iron Ingot · 12.5/min*, or *Water · 500/min · pipe*.

- **Loads.** When the problem is made, each link gets its loads (`BeltLoadDocument`: item, per minute, pipe or not): per item, the rate filled onto belts one after the other up to the top tier's capacity (`SupplyBelts.Loads`: 1500 is 1200 and 300). The loads go out with the problem (`ArrangementLinkDocument.Loads`) and come back in the stored run.
- **Which belt is which.** The router numbers a link's belts as it lays them, from 0, and its pipes apart (`RoutedBelt.Index`). The run's response pairs belt *n* with the link's *n*-th load of its kind (`ArrangementEndpoints`, `LoadOf`). The analysis service needs nothing new: it never looks at loads.
- **A change on the way.** Links between factories now add up their rate per item before rounding to belts, as links to nodes and stations already did; before, each source was rounded on its own. *I chose to fill belts one after the other (1200 + 300) rather than share evenly (750 + 750): the same rule as merging extractors. Overrule if you'd rather balance them.*
- **Map.** Each belt has a second, invisible line 10 pixels wide, so the thin line is easy to point at; the tooltip uses the catalog's item names (`carries`, `arrangement-model.ts`).

Your area (run `01a1ef17`): all 124 routed belts and pipes say what they carry. Singularity Cells, for example, takes Nuclear Pasta 4/min, Limestone 1200, Iron Ore 600, Dark Matter Crystal 80, and Water 500 twice by pipe; it sends Singularity Cells 2.5/min to its drone port and 4.825/min to a factory. Runs from before loads say *From a run before loads*.

Tests: `SupplyBeltsTests` +1 (what an item needs more than one belt for fills them one after the other) · `BeltRouterTests` +1 (a link's belts are numbered from 0, and so are its pipes) · `WorldArrangementTests` +1 (each belt of a link carries its item at its rate) · API: each routed belt of a computed run says what it carries · web +3 (`carries`: names the item and its rate; says so for a pipe; nothing for a run from before loads). **Totals:** 968 .NET tests, 106 web tests.

## Crossing over, straight across (2026-11-18)

You asked why a blue line made a curl at Pressure Conversion Cubes. Two belts, Singularity Cells to Ballistic Warp Drives and Gold Ore to Circuit Boards, had to get past the belt from Pressure Conversion Cubes to their drone port. They couldn't cross it, so they went round its lift: the only way past a belt without crossing. You chose: **crossing over is allowed, but only if the level doesn't need to change**.

- **The rule as built.** A belt may cross another only where **both run straight through the square, at right angles**. Neither turns there, and since levels change only at turns, neither changes level; the one-belt-to-a-level rule puts them on different levels. A crossing costs as much as two turns, so belts still run beside each other when that's as short.
- **How.** The bracket check (`Board.Crossings`, was `Nests`) now goes pair by pair: walking round the square, the new belt may interleave with another belt only when both go straight across each other; any other interleaving is still a crossing that isn't allowed, or a swap in a stack. A set of pairs nests like brackets exactly when no two interleave, so for squares without crossings nothing changes.
- **Pipes** still stay off squares of belts and the other way round: a stack is of one kind. *I read that as covering crossings too; say if a pipe may cross over a belt.*
- **One old test changed its meaning.** *Two belts in a stack can't swap sides* expected one of them to fail. Now the router finds a third way: one crosses over the other, straight across, west of the stack, and joins it on the right side. The test now pins that: no swap in the stack, a crossing outside it.

Your area, in the model (run `01a1ef27`): **130 routed, 10 of them pipes, none left over** (was 6). **36 km** together (was 47 km; 30 km if every belt ran its direct grid distance), tallest stack 15, 3 ramps, no lifts at turns. Belts that turn back on themselves: 32 of 130 (was 51 of 124). The run took 63 s.

Tests: `BeltRouterTests` +3, one renamed: a belt crosses over another where both run straight, on another level · belts never cross on one level (a ceiling of one) · a straight belt never crosses another where that one turns · a belt never crosses another where either turns · two belts in a stack never swap sides: one crosses over the other outside it. Breaking each condition of the rule for a moment (the crossing belt turns; the crossed belt turns; the two run parallel) fails at least one test. **Totals:** 971 .NET tests, 106 web tests.

## The page: delete runs, Reset, 3D over the page (2026-11-18)

You asked for delete buttons on earlier runs, a Reset in place of *Draw again*, *Keep out* and Reset closer to the map, and the 3D view as a pop-up.

- **Delete a run.** `DELETE /api/v1/world/arrangements/{id}`: 204, or 404 for an unknown run. A run still being computed can be deleted too: when the analysis service sends its proposal back, World answers 404, which the service already takes as "gone, nothing to store" (as for a deleted snapshot). On the page: a bin at the end of each run's row, asking first (`ArrangementDeletion`, as `FactoryDeletion` does for factories). BR-O1 now says runs are kept *until the player deletes them*.
- **Reset.** *Draw the area* until an area is drawn, then *Reset*: no area, no exclusions, no run shown; the settings stay. Picking a run still fills in its area and settings.
- **Closer to the map.** *Draw the area* / *Reset*, *Keep out* and *Belt floor in 3D* sit in a bar right above the map; the settings form keeps the numbers and *Arrange*.
- **3D over the page.** *Belt floor in 3D* opens it in a dialog of 95 % by 90 % of the window (`BeltFloor3dDialog`), with a close button. The toggle and the view under the map are gone. *I used a dialog: the factory 3D viewer is a page of its own (`/snapshots/{id}/area`), so "like it" could also mean a page; a dialog keeps the run you're looking at.*

- **Tooltips in 3D** (asked afterwards). Pointing at a belt or pipe tells what it carries, at a factory its name and how far it moves, the same text as on the map (`carries`, and `factoryTooltip`, now shared by both). Each belt is a mesh of its own again (merged per colour before), so a ray from the pointer finds it; a belt wins over a factory's slab, which would hide the belts under it from above. No tip while dragging. Web +2 (`factoryTooltip`: names it and how far it moves; says when it stays).

- **The level in the tooltip** (asked afterwards). Belts and pipes also say their level, from 0 on the floor (1 on one stacked belt, 2 on two): *Iron Ingot · 12.5/min · level 2*, or each in turn where it changes, *levels 0 → 1*. The response carries the levels themselves now (`RoutedBeltResponse.Levels`), not only heights in metres, so the page doesn't divide by 2 m. One text for map and 3D (`beltTooltip`, with `levelText`). API: every belt has a level per straight run; web +4 (one level all the way · each level in turn where it changes · nothing before levels · what a belt carries and its level). **Totals:** 979 .NET tests, 112 web tests.

- **Level by level in 3D** (asked afterwards, to build from level 0 up). The 3D window has a slider *Up to level n*: the levels above n are hidden, level n is drawn in full, the levels below are faded as built; *All levels* shows everything again. Each belt is drawn in pieces, one per level (`beltStretches3d`): a run on its own level, a ramp or lift on the lower of the two it joins, its lifts at the ends on the level they reach. *I chose to fade the levels below rather than hide them, so you see what the new level joins.* Web +3 (on its level all the way, its lifts too · a ramp on the lower level it joins, what follows on the higher · on the floor for a run from before levels). **Totals:** 979 .NET tests, 115 web tests.

Tests: API +2 (a run can be deleted even while it's computed · deleting an unknown run is not found). The page changes have no tests: there's no spec for the page component, only for its model. **Totals:** 973 .NET tests, 106 web tests. **Not tried in a browser.**

## A run that can't fit (2026-11-18)

Two of your runs came back strange: 11 belts not routed, the factories where they stand. *Road north-south* was 42 foundations (336 m) instead of 4, which left 18 plots, part of them kept out, for 48 factories. The solver found no layout (*None found*), and the run then showed the factories where they stand now as if that were the proposal, with belts routed between them. Your typo; the page didn't make it clear.

- **Refused at once.** `ArrangementProblem.FreePlots` (the area's plots that no station, other factory or exclusion blocks) and `HasRoom` (at least as many free plots as the factories take). A request makes the problem straight away (`ArrangementProblems.MakeAsync`, shared now with the analysis service's fetch) and answers 400 when there's no room: *48 factories but 7 free plots in the area: make the plots or roads smaller, the area larger, or keep less out.* No run is stored. The page shows the message next to *Arrange*.
- **No layout, nothing placed.** If the solver still finds none (time, or factories changed in between), the proposal has no places and no belts are routed (`ArrangementSolver`, `ArrangementDocuments`); the status reads *No layout found: nothing placed*. Runs from before keep what they showed.
- **Fast enough for a click.** Making the whole problem took 15 s on your world, nearly all of it each factory's inputs and outputs from the analysis. The check needs only where things stand, so `WorldArrangement.Placing` makes the problem without links (the same code, without the analysis): about 2 s. *I chose to check at the request rather than when the analysis service fetches the problem, so the answer comes with the click, not a run later.*
- **On your world:** your mistaken settings get *48 factories but 6 free plots*. With the road back at 4 foundations the same area and keep-out still get *48 factories but 45 free plots*: that area is a little too small. Your earlier, larger area arranges as before (run `01a1ef57`: all 130 belts and pipes routed, 27.5 km, tallest stack 4).

Tests: `ArrangementProblemTests` +2 (the free plots are the area less what blocks it · factories have no room when there are fewer free plots than they take) · `ArrangementSolverTests` +1 (without a layout nothing is placed) · `ArrangementDocumentsTests` +1 (without a layout no belts are routed) · `WorldArrangementTests` +1 (where things stand is the problem without its links) · API +1 (a run without a free plot for every factory is refused with the reason). **Totals:** 979 .NET tests, 106 web tests.

## How it works

```
CP-SAT places the factories (step 47)
  → BeltRouter.Route(floor, area, places, links)        World.Domain, no library
      squares of one foundation; per side between two squares, the belts across it in order
      per belt: A* over (square, side it came in by, place in that side's order)
      a belt through a square may only go where it nests with the belts there already
  → the proposal document carries belts (corner squares), unrouted links, tallest stack
World turns squares into centimetres: ArrangementGrid.WorldOf(square, floor)
```

**The no-crossing rule as brackets.** Going round a square (south side west to east, east side south to north, north side back, west side back), each belt through it shows up twice. They mustn't cross, and a stack keeps its order exactly when the sequence nests like brackets: `a b b a` is fine, `a b a b` is two belts crossing, or swapping sides in a stack. Checking a new belt is a scan of at most 2 × 16 entries with a stack. A lift shows only once in its square, so nothing nests with it, and that's what keeps every other belt off a lift.

**Why the search state includes a "place in the order".** Entering a square that already holds three belts, a new belt can go in at any of four places: below all, between, or above all. The place decides which way it may leave. So the A\* state is *(square, side it came in by, place)*, and the search finds the one gap that lets it through. It's as fine as the belts already there need, no finer.

## Files

| Where | What |
|---|---|
| `World.Domain/Arrangement/BeltRouter.cs` (new) | The router; `file`-local `Board` (the ordered sides, the search) and `State` |
| `World.Domain/Arrangement/BeltFloor.cs`, `FloorSquare.cs`, `RoutedBelt.cs`, `BeltRouting.cs` (new) | The floor (metres → squares, stack height), its squares, a routed belt, the result |
| `World.Contracts/Arrangement/BeltFloorDocument.cs`, `FloorSquareDocument.cs`, `RoutedBeltDocument.cs` (new); `ArrangementProblemDocument`, `ArrangementProposalDocument` | The floor goes out with the problem; belts, unrouted links and the tallest stack come back. All optional, so older runs still read. |
| `World.Simulation/ArrangementDocuments.cs` | Routes right after placing, when the problem has a floor |
| `World/Arrangement/*` | `CeilingMetres` on request, settings, stored run, summary (with `BeltMetres`, `TallestStack`, `UnroutedBelts`); `ArrangementResponse.Belts`, `.Unrouted`; `RoutedBeltResponse`, `WorldPoint`; `ArrangementGrid.WorldOf(square, floor)` |
| `World/Persistence/Migrations/*_ArrangementBeltFloor.cs` (new) | `ceiling_metres`, 32 for older runs |
| `web/src/app/world/arrangement/*` | *Belt floor (foundations high)* setting; *Belts routed* column (length, tallest stack, how many not routed, in red); the belts on the map, thin in their source factory's colour, with a dot at each lift; *Belts on the floor below* toggle; picking a factory makes its belts stand out |
| BR-O2, ADR-0028, glossary (*Belt Floor*, *Stack*, *Lift*), primer | The rules, the decision, the terms |

## Tests

Domain (`BeltFloorTests`, 4, and `BeltRouterTests`, 10, written first): an area of plots is its squares with the roads · a factory stands over its plots and the roads between them, not the road after · metres become squares of one foundation, a stack a belt per 2 m of ceiling · a low ceiling still holds one belt · a belt runs straight from lift to lift · of the shortest ways, the fewest turns · a link gets as many belts as it needs · every belt has a lift of its own · a belt never passes a lift · belts never cross; one that can't go around is reported · **two belts in a stack can't swap sides** (your example: a gap between lifts forces them into one stack, with their ends interleaved) · belts stack where there's no room beside · a stack holds no more than fit under the ceiling · a link with a factory outside the area isn't routed. Simulation (`ArrangementDocumentsTests`, +2): belts are routed on the floor · a problem from before the floor gets none. API (`WorldArrangementApiTests`, +2): the belts of a computed run, under the ceiling asked for · a ceiling must leave room for a belt. Web (`arrangement-model.spec.ts`, +2): the ceiling in foundation heights · a belt's corners on the map.

**I checked the tests catch what they claim** by breaking the code for a moment: without the nesting check both crossing tests fail; without the ceiling the ceiling test fails; without the turn cost the turns test fails. That showed two weak spots, now fixed: the swap test first passed for the wrong reason (a belt took the other factory's only lift square), and on an empty floor A\* finds L-shapes even without a turn cost, so the turns test now has lifts in the way. An explicit "no belt on a lift" check turned out to be redundant with the nesting, so it's gone, with a comment where the nesting covers it.

After the first look: one plot per factory (`WorldArrangementTests`, replacing *whole plots around its machines*); a link to a node or station counts at its weight (`ArrangementProblemTests`); a lightly weighed node draws a factory less than the factory it feeds (`ArrangementSolverTests`); a belt from a node comes up at its extractor, a belt to a station goes down at its platform, a belt from a spot outside the area leaves at the edge facing it (`BeltRouterTests`); ore from a miner is a link at the nodes' weight from where it stands (`WorldArrangementTests`); belts from spots are routed (`ArrangementDocumentsTests`); weights can't be negative (API); which end of a belt is a spot (web). Each new rule was checked to fail without its code.

**Totals** (then): 953 .NET tests, 99 web tests. **The page hasn't been tried in a browser** (neither has step 47's).

## Points worth noticing

| | Java / Spring | Here |
|---|---|---|
| A priority queue | `PriorityQueue<E>` with a `Comparator` on the elements | `PriorityQueue<TElement, TPriority>`: the priority is separate, here a value tuple `(estimate, left)` that breaks ties by its second item. No decrease-key in either: queue again, skip the stale copy. |
| A helper class only one class uses | A package-private top-level class (the package still sees it) | `file sealed class Board`: visible only in `BeltRouter.cs`. The one exception to one type per file. |
| A small value type used as a key by the thousand | A record (always on the heap) | `readonly record struct State(int Square, int Side, int Slot)`: a value type with value equality, no allocation |
| Optional fields added to a JSON contract | Jackson ignores missing fields if configured | Positional records with defaults (`BeltFloorDocument? Floor = null`): System.Text.Json passes the default when the field is missing, so stored older runs still read |
| A migration's default for existing rows | Flyway: `ADD COLUMN … DEFAULT 32` by hand | EF generated `defaultValue: 0.0` (the CLR default); changed by hand to `32.0` so older runs read as the 32 m they implicitly had |

## Choices worth a second look

1. **Greedy order**: which link goes first decides who goes round whom. Rip-up and reroute now tries up to 8 orders; a belt can still be left over that another order would route.
2. **Heights** (now done): a level per straight run, ramps and lifts where it turns. With nothing needing a climb on your area, how ramps and lifts look is untried on real data.
3. **A lift anywhere under its factory**: maybe you want lifts at the factory's edge, or on its road side.
4. **Belts may run under other factories** (you said free everywhere): fine on a floor of its own, but it means a later lift for that factory may find its squares taken.
5. **Test runs of yours are in the database now** (`01a1ee0a…`, `01a1ee37…`, `01a1ee41…`), kept like all runs.
6. **A spot's lifts need free squares on it**: a drone port with many belts runs out; lifts beside it could be allowed.
7. **Factories outside the area** still get no belts; nodes and stations outside do (to the edge). Same treatment for them?
8. **Fixed factory places** outside the area still take the plots they touch, not one plot.

## Next

You choose: try the page and the 3D view, then perhaps lifts down to a transport layer (stations), or lifts beside a crowded spot.
