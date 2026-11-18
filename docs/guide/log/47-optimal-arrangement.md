# Step 47 · Optimal arrangement

**Goal:** a proposal for where the world's factories would best stand, so the belt work between them and their inputs (other factories, stations, resource nodes) is least (asked 2026-11-12). Built in three parts: the model and solver, the problem from your world, the service and the page. Branch: `main`, uncommitted.

## What you decided (2026-11-12)

- **Cost:** per input, the **belts it needs** (rate over a top-tier belt's or pipe's capacity, rounded up) **× the distance along the grid** between the centres.
- **Fixed places:** resource nodes, train and truck stations and drone ports don't move. Stations and drone ports take their space; **a node doesn't**: factories stand on platforms above it, with the extractors below.
- **A factory's space:** first *its machines and own belts*; after the probe on your world showed that makes Rocket Fuel 1.8 × 1.5 km and the proposal worse than now, **around its machines only**. Moved, never turned.
- **Where they may go:** first inside the rectangle around everything built, with room between factories configurable along x and y; after the first look, **plots in rows and columns inside an area you draw** (below).
- **Computed in the analysis service**, like the flow analysis; **every run kept**.

All of it is BR-O1; the architecture is ADR-0027.

## After the first look (2026-11-12)

You saw stations everywhere, a web of lines, and factories placed freely. You chose:
- **Plots in rows and columns**, like your world: the area is divided into equal plots with roads between them; a factory takes as many whole plots as its machines need (`⌈(size + road) ÷ (plot + road)⌉` along each axis). In the solver nothing changed: a cell is now a plot with its road, and there's no gap besides the roads. The settings are plot width, plot depth, road east-west and road north-south (80, 80, 16, 16 metres by default).
- **Draw the area to arrange:** only the factories whose machines' middle is inside it are arranged, and they stay inside it. Every other factory stays, and keeps the plots it touches; links to it still count.
- **Links of the factory pointed at**, not a web of them all. The stations are drawn where they stand (247 freight platforms and 28 drone ports in your world): they never moved, they only block their plots.

The stored runs got the new settings (migration `ArrangementPlots`, which also removes the three runs made on cells while this was built). On your world: everything on 80 m plots, 1,818 → 1,302 (−28%), 8 of 72 stay; a 1 km area around Crystal Oscillators, 44 factories, 1,081 → 810 (−25%). The cost is now in plots, so it doesn't compare with the first runs' cells.

## After the second look (2026-11-12)

The grid was right, but the map was cluttered: many lines on some factories, the *now* frames didn't hold their machines' dots, and the side bar only listed distances. Now:
- **A clean grid first:** with a run shown, only the area and the proposed places show. The machine dots, the frames of where factories stand and the move lines are off.
- **The side bar is the controls:** *Where they stand now* (machines and the rectangle around them) and *Moves* (a line from there to the proposed place), both off by default; below them every factory, the farthest move first. Picking one (there, or by clicking its place on the map) shows only its place now, its move and its links; pick several to compare.
- **The frame of where a factory stands** is now the rectangle around its machines as built, not the plots it touches, so its dots are inside.
- **Links:** a line only to the factories it's linked with, thicker for more belts (1 + log₂ belts). Nodes and stations that feed it get a ring where they stand, no line: they don't move, so the ring says enough.

## Kept together (2026-11-12)

Some factories stood far from the others: a factory whose inputs are distant nodes, stations or factories outside the area was pulled to the area's edge nearest them. You chose to **keep them together**: the solver adds the spread, the width plus the depth in plots of the rectangle around all arranged factories, times a weight, the setting *Keep together* (2 by default; 0 is the old behaviour). Only the factories on the edge make that rectangle bigger, so it pulls in exactly the outliers. The shown cost stays the belt work alone. Older runs keep 0 (migration `ArrangementTogether`).

Your area (48 factories), 60 s each, in the model:

| Keep together | Belt work now → proposed | Spread of the proposal | Farthest move |
|---|---|---|---|
| 0 | 1,023.5 → 683.5 | 848 × 1,136 m | Silica 1,152 m |
| 2 | 1,023.5 → 701.5 | 848 × 848 m | Thermal Propulsion Rockets 864 m |
| 10 | 1,023.5 → 680.5 | 752 × 848 m | Electromagnetic Control Rods 960 m |

Two things to notice. The solver stops at 60 s with a good layout, not the best (*feasible*), so runs differ by a few percent: weight 10 found less belt work than 0. And the factories as built span 656 × 656 m, yet the proposals don't fit that: a factory takes whole plots of its own, while yours stand closer, sharing plots, so on 80 m plots they need more room than they take now. Smaller plots would close that gap.

## Only factories (2026-11-12)

Pointing at a factory drew lines to some inputs and white rings for others, often in odd places: a ring stood for a node (not drawn on the map), a station or a factory outside the area, in the middle of the plots it touches. You chose to **count only inputs from other factories** as a first step: the goal is that factories related by what they make and use stand close together; how far a node or station is doesn't matter now. **Stations still block** their plots and still show on the map.

- `WorldArrangement` keeps an input only when it comes from another factory; nodes and the buildings that feed a factory are no longer fixed places. The fixed places are now the factories outside the area and the stations, all blocking.
- Pointing at a factory draws a line to each factory it's linked with: solid to one that's arranged too, dashed to one outside the area, which stays.
- The domain and solver are unchanged: a fixed place that doesn't block still works there, it just isn't made from a world any more.
- Older runs keep the links they were computed with.

Your area again, keep together 2, in the model: 67 links between factories, belt work 224 → 116 (−48%), the factories in 752 × 752 m, 3 of 48 stay; the farthest move Dark Matter Crystals, 864 m.

## Exclusions, and a calmer hover (2026-11-12)

You asked to drop the *Moves* toggle and the dashed lines (to factories outside the area, which read like lines to nodes and stations), and to keep factories off places inside the area, such as where trains come in, with a new calculation.

- **Exclusions:** *Keep out* draws a rectangle inside the area; the plots it touches are kept free, like a station's. Click one to remove it. A run keeps its exclusions (`StoredArrangement.Exclusions`, a jsonb list, migration `ArrangementExclusions`).
- **A new calculation is a new run**, so every run stays kept. Picking a run makes its area, exclusions and settings the start of the next one: add an exclusion, press *Arrange*.
- **Pointing at a factory** draws lines only to the factories arranged with it. *Where they stand now* and picking factories in the side bar stay.

Tests: an exclusion keeps factories off the plots it touches (`WorldArrangementTests`); its exclusions are kept with the run, and an exclusion must have some size (`WorldArrangementApiTests`).

## Plots in foundations (2026-11-12)

The plots looked large: 80 by 80 metres with roads of 16 is 10 by 10 foundations with roads of 2, while you build factories on 6 by 6 foundations with roads of 4. The page now asks for plots and roads **in foundations** (8 metres) and shows them so in the runs table; **6, 6, 4, 4** by default, also in the API. The API and the stored runs stay in metres: the page converts (`metres`, `foundations` in `arrangement-model.ts`), so older runs show as 10 × 10 and 2.

## How it works

```
web ─POST /snapshots/{id}/arrangements {cell, room x, room y, seconds}─► World: pending run + ArrangementRequested (outbox)
analysis service ◄─ RabbitMQ world.arrangements-to-compute
   GET /internal/world/arrangements/{id}/problem ─► WorldArrangement.Of: factories, links, fixed places, area (~20 s)
   ArrangementDocuments.Solve ─► ArrangementSolver (CP-SAT, the run's seconds)
   PUT /internal/world/arrangements/{id}/proposal
web ─GET /arrangements?world=… · /arrangements/{id}─► runs; one run's rectangles now and proposed, in centimetres
```

- **The model** (`World.Domain/Arrangement`): `GridRectangle`, `ArrangedFactory`, `FixedPlace` (with `Blocks`), `ArrangementLink`, `ArrangementProblem` (with the cost of a layout), `ArrangementProposal`. All in grid cells; centres doubled (`2x + width`) so distances stay whole numbers for CP-SAT.
- **The solver** (`World.Simulation/ArrangementSolver`): every factory and blocking place is a rectangle widened by the room on its far sides, and none overlap (`NoOverlap2D`); blocking places that overlap each other are merged first. The objective is belt work first, then how far factories move (weighted so one cell of belt work outweighs all moving), so a factory that saves nothing by moving stays.
- **From your world** (`World/Arrangement/WorldArrangement`): a factory's rectangle from its machines' footprints, turned by their yaw; its links from the input table (`FactoryInputs`, BR-A19); the cell and the room converted from metres.

## On your world

| | |
|---|---|
| Problem | 72 factories · 500 fixed places (275 blocking) · 436 links (442 belts) |
| A run | about 90 s: 23 s to make the problem, 60 s to solve, the rest messaging |
| Belt work | **22,016 now → 14,444 proposed (−34%)**, the best found in 60 s |
| Moves | 71 of 72 factories; the farthest Adaptive Control Units 756 m, Crystal Oscillators 754 m |

Two first runs never finished: Wolverine cancels a handler after 60 seconds by default, and the service's HTTP client cut every attempt at 10 seconds (Aspire's standard resilience handler). Both are raised for the service (ADR-0027); the two runs are marked failed with that reason.

## Files

| Where | What |
|---|---|
| `World.Domain/Arrangement/*` (new) | The model |
| `World.Simulation/ArrangementSolver.cs`, `ArrangementDocuments.cs` (new) | CP-SAT; the service's side |
| `World.Contracts/Arrangement/*` (new) | `ArrangementRequested`, the problem and proposal documents, `ArrangementQuality`; the queue name in `AnalysisMessaging` |
| `World/Arrangement/*` (new) | `WorldArrangement`, `ArrangementGrid`, `ArrangementSettings`, the stored run, public and internal endpoints, responses |
| `World/Persistence/Migrations/*_AddArrangements.cs` (new) | `world.arrangements` |
| `World/WorldModule.cs`, `Analysis/AnalysisMessages.cs`, `WorldDbContext.cs` | Endpoints, the message's route, the table |
| `AnalysisService/ArrangementRequestedHandler.cs` (new), `WorldClient.cs`, `AnalysisService.cs` | The handler, its calls, its queue and the raised limits |
| `web/src/app/world/arrangement/*` (new) | The page (draw the area, the runs, the map), `arrangement-model.ts` |
| `World/Persistence/Migrations/*_ArrangementPlots.cs` (new) | Plots, roads and the area instead of cells and gaps |
| `World/Persistence/Migrations/*_ArrangementTogether.cs` (new) | The *Keep together* weight per run |
| `World/Persistence/Migrations/*_ArrangementExclusions.cs` (new) | The exclusions per run |
| `web/src/app/app.routes.ts`, `snapshot-page.html` | The route; *Optimal arrangement* on a snapshot |
| BR-O1, ADR-0027, glossary | The rules, the decision, *Optimal Arrangement*, *Fixed Place*, *Arrangement Run* |

## Tests

Domain (`ArrangementProblemTests`, written first, 4): a link costs belts × grid distance between the centres · a link to a fixed place counts · a layout costs what its links cost there · a factory needs room. Solver (`ArrangementSolverTests`, 8): two linked factories end up side by side · factories never overlap and keep their room (2 along x, 3 along y) · drawn next to a station that feeds it but never onto it · may stand over the node that feeds it · stay inside the area · never cost more than now · one that saves nothing by moving stays · factories kept together stand side by side, though their inputs are far apart. `ArrangementDocumentsTests`: a problem document is solved into a proposal for every factory. From the world (`WorldArrangementTests`, 6, test save): a factory takes the whole plots around its machines · only the factories inside the area are arranged, the others stay and take their plots · ore from a miner is no link, only inputs from factories count · ingots from another factory weigh the belts they need · a freight platform in the area blocks its plots · an area holds as many plots as fit with the roads between them. API (`WorldArrangementApiTests`, 5, through RabbitMQ and the service in-process): a requested arrangement is computed in the background and kept · every run is kept for its world · its plots must have a size and its roads can't be negative · keeping together can't weigh less than nothing · an unknown snapshot is not found. Web (`arrangement-model.spec.ts`, 5): a rectangle on the map · how far a factory moves · the saving as a share · nothing to save · the area from two corners dragged either way.

The service's raised limits have no test: the test world's problem is made in well under a second. **The page hasn't been tried in a browser yet.**

**Totals:** 924 .NET tests, 96 web tests.

## Points worth noticing

| | Java / Spring | Here |
|---|---|---|
| A constraint solver | OptaPlanner / Timefold, or OR-Tools' Java API | OR-Tools' C# API: `CpModel`, `NewIntVar`, `NewFixedSizeIntervalVar`, `AddNoOverlap2D().AddRectangle(x, y)`, `Minimize` |
| A handler's time limit | `@Transactional(timeout=…)`, a listener container's settings | Wolverine's `DefaultExecutionTimeout`, a `CancellationToken` the handler passes on |
| Replacing a default HTTP policy | Resilience4j per client | `RemoveAllResilienceHandlers().AddStandardResilienceHandler(…)`; marked for evaluation, so its warning (EXTEXP0001) is suppressed where it's used |

## Choices worth a second look

1. **Most factories in the area move**: the solver shortens the belts, not your rebuilding work. A smaller area gives smaller changes.
2. **Without terrain**, a proposed place may be water or a cliff; check on the map.
3. **A run that's never answered stays pending**: unlike analyses, runs aren't requeued at startup.
4. **A deleted factory** still shows in old runs, as *(deleted)*.
5. **Building the problem takes ~20 s**, mostly the input tables of all factories with their *Short* max-flows, which the arrangement doesn't need; a lighter path could make it seconds.
6. **A factory whose middle is just outside the area** stays, though some of its machines are inside: it keeps the plots it touches.

## Next

You choose.
