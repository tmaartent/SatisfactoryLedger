# Step 49 · Supply lines with priority mergers

**Goal:** stop giving every factory its own belt from its own miners. What brings an item runs as one line past the factories that need it, topped up by a priority merger where it runs short. It is a toggle on the arrangement run (asked 2026-11-18). Branch: `feat/optimal-arrangement`, on top of step 48 (committed first), uncommitted.

Your example: miners give 1200 iron ore. The factories take 500, then 400, then 600. The line carries 1200 to the first, 700 to the second and 300 to the third. Just before the third, a priority merger tops it up from other miners.

## What you decided (2026-11-18)

- **Every kind of source** runs as supply lines:
  - nodes' extractors
  - stations and drone ports
  - a factory's output to the factories (and stations) it feeds
- **Routing only.** Placing the factories stays as it is; only the belts on the floor change.
- **Pipes are taps only.** One pipe serves factories in turn until it runs dry, then a new pipe starts. There are no junctions merging into a running pipe.
- The rules you set for supply lines earlier still hold:
  - Taps are a manifold (BR-S3).
  - At an injection, the line's own flow goes first (BR-S4).
  - Pipes have no injections (BR-S11).

All of it is in BR-O2; the routing side is in ADR-0028.

## What I chose (overrule any)

| Choice | Why |
|---|---|
| **A line starts at the largest source left** | Big sources first means fewer, fuller lines. |
| **It goes to the nearest consumer each time** | Simple and short. It doesn't look at what's in the way on the floor; that's the router's job. |
| **An injection comes from the source nearest the factory that would come short** | That's where its merger stands, so its belt is shortest. |
| **An injection fills the line up to the belt and to what's still needed**, not just what this factory lacks | The factories after it are fed too, with fewer mergers. In the test: 200 left, the next factory needs 500 and the one after 300, so the merger brings 600 rather than 300. |
| **A line carries no more than the consumers left take** | In the game a belt only moves what's taken downstream; the rest backs up. |
| **A factory that needs more than a belt** gets a second line | The first line gives it a full belt and ends, and the next line brings the rest. |
| **The extractors of an item still merge onto belts first** (as in step 48), whichever factories they fed in the save | A group of miners is one source, one lift. |
| **A station a factory sends to is a consumer on that item's line** | It's tapped like a factory. |
| **The toggle is off unless chosen**, and stored with the run | Earlier runs keep their meaning. |

## How it works

1. **World module** (`WorldArrangement.Make`): with the toggle on, it adds every item's sources and consumers to the problem as `ItemSupplyDocument`s. These are the pooled extractor groups, the factories sending to others, the factories taking the item, and the stations taking it. Placing still uses the links.
2. **Analysis service** (`ArrangementDocuments.Solve`): it places the factories with CP-SAT as before. Then, now that it knows where each factory stands, `SupplyLines.Plan` turns each item into **legs**. A leg is one belt from one end to the next, either along the line or as an injection.
3. The legs are grouped into links by their two ends and routed by the **unchanged** `BeltRouter`. Each routed belt now carries its **load** (`RoutedBeltDocument.Load`, with `Injection`), because the legs aren't the problem's links.
4. **API:** a belt's tooltip data now comes from the service. Runs from before still pair belts with their link's loads, as in step 48.
5. **Page:** a *Supply lines* toggle in the form, remembered with each run. The runs table shows "· supply lines". A belt that tops up a line says "into a priority merger" in its tooltip, on the map and in 3D.

In the model, a line through a factory is two belts: one in, one out. In the game that's a splitter at the factory, and a priority merger where an injection ends.

## Files

- Domain:
  - `World.Domain/Arrangement/SupplyLines.cs` (the planner)
  - `SupplyEnd.cs`, `ItemSupply.cs`, `SupplyLeg.cs`
- Contracts:
  - `ItemSupplyDocument.cs`, `SupplyEndDocument.cs`
  - `ArrangementProblemDocument.Supplies`
  - `BeltLoadDocument.Injection`
  - `RoutedBeltDocument.Load`
- Service: `World.Simulation/ArrangementDocuments.cs` (`Lines`, each belt's `LoadOf`).
- World:
  - `MadeArrangement.cs`, which is new. It replaces the four-value tuple from `WorldArrangement.Of` and builds the document (`ToDocument` moved there).
  - `WorldArrangement.cs` (`SuppliesOf`)
  - `ArrangementSettings`, `ArrangementRequest`, `StoredArrangement`, `ArrangementSummary`: `SupplyLines`
  - `RoutedBeltResponse.Injection`
  - `ArrangementEndpoints`, `InternalArrangementEndpoints`, `ArrangementProblems`
  - migration `ArrangementSupplyLines`
- Web:
  - `arrangement-model.ts` (`carries` says "into a priority merger")
  - `arrangement-page.html/.ts` (the toggle, the runs column)
- Tests:
  - `SupplyLinesTests` (9): your example, nearest first, nearest injection, filling for the factories after, no more than taken, more than a belt, pipes, never feeding itself, ends off the floor.
  - `ArrangementDocumentsTests` (+3): each belt carries its link's load, a line instead of a belt to each, an injection routed to its merger's factory.
  - `WorldArrangementTests` (+3).
  - `WorldArrangementApiTests` (+1).
  - web spec (+1).
- Docs: BR-O2, glossary (*Supply Line*, *Injection*), ADR-0028, primer.

Tests: **1014 .NET** (35 new: pipes followed, belts outside join the existing ones, joins on the floor, the accelerator footprint, supply lines, two for pipes crossing belts, two for the overflow, two for the machines endpoint), **140 web**, build clean.

## Not tried yet

Not on your world, and not in a browser: the AppHost was running when I built, and I stopped it to free the DLLs. Start it, then run your area once with *Supply lines* on and once without. Compare the belt metres, the stacks and what's left unrouted.

## The top-up's overflow runs on (2026-11-18, your option b)

You pointed out the game's trick: a **smart splitter** before the priority merger, with *Any* to the merger and *Overflow* on, so one top-up belt can feed several mergers. In the model, what a top-up source has left after a merger becomes a source **where that merger stands**. You chose (b): it may top up **any line of that item**, **start a line of its own**, or **feed that factory straight from the splitter** (no belt on the floor; a factory needing more than a belt gets its rest that way). The rule "a factory never feeds itself" now covers only its own product.

Each stretch of the top-up belt carries what's taken after it: the merger's draw and every later draw from its overflow. Each source keeps the top-up belt that brought it, so a later draw adds to every stretch before it (`SupplyLines.Source.Feeder`, `Leg.Before`). Two tests: the overflow tops up the next merger (the top-up belt to the first merger carries 300 + 300), and it starts a line and feeds its own factory. Both fail when the stretches aren't added to.

## The factories drawn with their machines (2026-11-18)

You asked to model the factories as the map view does, and chose drawing (placing still takes one plot per factory). On the arrangement's map and in the 3D pop-up, each proposed factory now shows its **machines at their footprints**, facing the way they do now, keeping their places to each other, **the rectangle around their footprints in the middle of the proposed plot** (`placedMachines`). The first try moved them by the shift between the plot the factory is on now and the proposed one, and you saw them sit off their plots: the plots start at the drawn area's corner, not where your 6×6 blueprints were placed, so each factory carried its own offset along. Every factory is a blueprint that fills its plot, so centring the machines is the right reference. The footprints count turned the way the machines face (`footprintWorldCorners`, which the map's `footprintCorners` now uses too). In 3D they stand on the factory's slab at their heights over its lowest machine, and pointing at them gives the factory's tooltip.

- **New endpoint** `GET /arrangements/{id}/machines` (`MachinesAsync`, `ArrangementMachinesResponse`, `ArrangedMachineResponse`): each arranged factory's machines (class, position, yaw) and the footprints of their classes, the same data the map gets. A stored machine keeps no yaw, so it's read from the run's snapshot. That's slow enough that it's its own endpoint, asked **once per ready run**, not with every look at the run, which the page repeats every two seconds while one is computed.
- The area page's `boxGeometry` moved to `area/box-geometry.ts`, shared with the 3D view, which reuses `buildingBoxes` (the footprint's volumes, turned by yaw).
- Tests: 2 API (the machines and their footprints; an unknown run), 2 web specs (the machines stand in the middle of the plot keeping their places; footprints count turned).

## The Particle Accelerator's footprint (2026-11-18)

You saw the accelerators overflow their plots, on the arrangement and so on the map too. The catalog takes a building's footprint from its clearance boxes in the Docs file, each turned and moved by its offset, and then the rectangle around them seen from above. `ClearanceDataParser` applied only the turn around the vertical (yaw). The accelerator's arch is boxes **tilted** around the y axis: counted flat they kept their full length, and for a tilt of a quarter or more `atan2(…, 1 − 2y²)` gave half a turn and mirrored the box. It covered 50 m east-west instead of 37.

The fix turns all eight corners of each box in 3D by its quaternion (`Turn`, the same formula as the 3D view's `rotate`), then takes the rectangle around them. Test: the accelerator covers −18 to 19 m by −14 to 13 m (fails on the old parser with 32 m east). The 3D views were already right: they draw the boxes themselves.

**To see it:** footprints are copied into a snapshot when the save is imported. Import the Docs file again, then the save; runs on older snapshots keep the old footprints.

## Boxes in 2D, splitters and mergers, machines off (2026-11-18)

- **Each building as its boxes seen from above**, on the map view and the arrangement: a Quantum Encoder at 45° is its cross, so it fits its plot as in the game, instead of the rectangle around it poking out. `fromAbove` (area-model) is the outline round a box's eight corners (Andrew's monotone chain, a convex hull); `footprintShapes` (map-model) replaces `footprintCorners` and builds on the 3D view's `buildingBoxes`. Leaflet takes `[ring, ring]` as a polygon with holes, so the boxes go in as `[[ring], [ring]]`, a multipolygon, filled `nonzero` so overlapping boxes don't cut each other out. Centring a factory on its plot uses the boxes too.
- **Smart splitters and priority mergers** on the map (an orange square, a blue diamond) and in 3D (cubes at the ceiling, where the belts go up into the factory), with tooltips: a priority merger where a top-up belt ends, a smart splitter where a belt carrying a top-up's overflow on starts (`lineJoints`). For that, a leg says whether it starts at a splitter (`SupplyLeg.Overflow`, carried as `BeltLoadDocument.Overflow` and `RoutedBeltResponse.Overflow`). A line from an overflow that first feeds its own factory keeps the mark for the belt that leaves the factory.
- **A *Machines* toggle in the 3D pop-up**: off, only the flat plots show.
- Tests: map specs (a cross of boxes is two shapes; a tilted box covers its ground), 4 `lineJoints` specs, the overflow marks in the two overflow domain tests.

## The top-up joins the line on the floor (2026-11-18)

You saw a "priority merger" on top of a lift with nothing merging into it: in the model the top-up belt went up its own lift into the factory, and the merge was somewhere above. Now **the top-up belt ends on the line's belt**, a few squares before the factory, and one belt goes up; the **smart splitter** is a square on the top-up's belt where the overflow leaves.

- **Plan:** each leg says which leg it merges onto (`SupplyLeg.Onto`) or whose splitter it leaves (`Off`), by number. The service numbers legs across all items, so it shifts each item's numbers (a test fails without that: limestone's top-up joined the iron ore line).
- **Router:** every belt is a job of its own (`Job`, a private record nested in `BeltRouter`: a `file`-local type can't appear in a member's signature of a public class, even a private one's). Joins are laid after what they join. The belt joined offers as an end the squares near its end where it's alone, keeps its level and has a free side (`Board.JointsOn`, up to 6 squares back); the new belt arrives there by a free side at that level, and the square is then taken whole, as a lift's. Where none fits, it goes to the factory as before. `RoutedBelt.StartsOnBelt` / `EndsOnBelt` say which.
- **Page:** no lift dot and no lift up in 3D at a joined end; the merger and splitter stand on the belt there, at its height. They're drawn unlit now (`MeshBasicMaterial`), so they keep their orange and blue in any light.
- Tests: 2 router tests (a top-up ends on the line before the factory, at the line's level; an overflow leaves the top-up at a splitter), the numbering in the plan's tests, the service test across items, 3 web specs.

## Belts outside the area join the existing ones (2026-11-18)

The yellow circles were where a belt to a building outside the area left the floor, at the edge facing the building. Your proposal: keep the circle as where the belt enters the area, but outside it **join the existing belt in the save** that goes to that building (you chose following the save's belts over a straight line).

- **World module** (`ExistingWays`, `SpotEntry`): for each spot outside the area, a breadth-first search along the save's conveyors (`WorldGraph.Downstream`, or `Upstream` for a station that's supplied) from the building to the first of the arranged factories' machines; the belts' curves as points; where that way crosses into the area (Liang–Barsky, the entering edge only). The spot goes to the service with its **entry** (a few edge squares round that point, so a station's belts can leave side by side) and the way outside.
- **Router:** a spot with an entry is reached there (`End.Leaves`): on the floor's edge if the entry is on the floor, else facing it, as before. Still marked as leaving the floor, so the yellow circle stays.
- **Page:** the existing belt outside the area drawn thin and dotted in the colour of the factory it serves, from the circle out to the building, on the map and as a line on the ground in 3D (`joinedWays`, `ArrangementResponse.Ways`).
- **The yellow circle on the area's edge** (after your next look): the floor is whole plots, so between the last plot and the drawn edge lies a strip without floor; the circles stood on the floor's edge and didn't meet the dotted way, which ends at the drawn edge. Now the circle stands where the existing belt comes in, on the drawn edge, the routed belt is drawn on across the strip to it, and the dotted way has a tooltip: the building it comes from (by the game's name, "Miner Mk.2 and 2 more" for merged ones) and what the belts joining it carry (`joinedWays`, `spotName`).
- **Belts running to their circle** (after you saw them leave by their own factory and cross the map in a straight line): an entry beyond the floor counted as off the floor, where every square of the floor's edge costs the same, so a belt left at the edge nearest its factory and the rest was one long line. Now the entry is projected onto the floor's edge (`Board.EdgeAt`): the belt runs along the floor, at right angles, to the edge by its circle, and the short stretch across the strip is drawn across it and then along the area's edge (`across`). A router test and a web spec.
- **Lifts in 3D only into factories** (after you saw three cyan uprights outside the factory): every belt end went up a lift to the ceiling, also at a miner, station or drone port (which stand on the ground) and at the floor's edge where a belt leaves to join an existing one. Now only an end at a factory lifts (`beltPath3d` with the arranged factories). A web spec.
- **Pipes** (after you saw them unconnected): the save reader joins a pipe network into one hub without a place (BR-A6) and dropped which pipes touch, so a pipe couldn't be followed. The import now keeps those links (`Wiring.PipeLinks`, `SavedWorld.PipeLinks`, `SnapshotDocument.PipeLinks`, **snapshot version 13**), outside the flow, and `ExistingWays` walks them both ways besides the connections. Older snapshots read as before, without them. Tests: the wiring keeps the links; a pipe is followed piece by piece through its network; a version 12 snapshot still reads.
- Tests: 3 for the ways (the crossing, none, the test world's miner to smelter A along its belt), 1 router test (the belt leaves where the existing one comes in), 1 World test (a miner outside the area gets its entry), 2 web specs.

## Joins anywhere along the belt (2026-11-18)

You found a lone Iron Ore overflow on level 9 whose splitter and merger stood up at the factories with nothing joining them. On that run (`01a1f086`) 3 of 10 overflows and 4 of 25 top-ups had fallen back to the factory: a join could only take the last 6 squares of the belt it joins, where it had to be alone and keep its level, and top-up belts are often short. That one had fallen back at both ends. Now a join may take any square of the belt but its ends; the search takes the nearest to where the joining belt comes from. Router test: a top-up from miners by the start of a long line joins it there, not by the factory.

## The existing belts outside, followed better (2026-11-18)

You saw a Rubber belt (and Plastic on level 5) from a train station drawn as a squiggle round the outside of the area, and said it doesn't look like that. Both come from groups of four freight platforms merged onto one belt. Three flaws:

- **Any arranged factory:** the search stopped at the first arranged factory's machine it reached, and a freight platform unloads onto belts of every item, so it could follow a belt to another factory. Now it aims at the machines of the factories that building supplies (or that send to it).
- **The first platform:** of buildings merged onto one belt it took the first with a way. Now the one whose way outside is shortest.
- **On the edge:** a building counted as outside when the middle of its outline was; one straddling the edge was followed round the outside. Now only one wholly outside (a World test with the miner's footprint, the edge a metre past its middle; it fails on the old rule).

- **The fewest buildings, not the shortest:** still wrong after those, because the search was breadth-first: the fewest buildings won, and a detour of a few long belts round the outside beat the direct way of many pieces (lifts, splitters, mergers). Now Dijkstra by the distance between the buildings' middles. A test with a direct way of three belts and a detour of one 500 m off: the detour won before.

- **From the belt, not the building's middle:** the way started at the building's position, its middle, so each freight platform had a short diagonal to its belt. Now it starts where the building's first belt does; the middle only turns that belt the right way round (or starts the way when that belt has no curve). Two tests now expect the miner's belt's start.

Also: in 3D a belt above the floor goes down a lift to the ground at a miner, station or the floor's edge (it stopped in the air, a gap to the line outside), and that lift goes with the belt's level; and the lines outside have their tooltip in 3D too (a line is found within a metre of the pointer). Heights outside are still flattened, and a lift's or pipe's direction still guessed.

## Whole belts in 3D, and a retry for joins that fell back (2026-11-18)

- **A ticked level shows whole belts:** a belt with a run on a ticked level shows whole, its pieces on unticked levels faded to a fifth, with its lifts, splitter or merger and the existing belt outside (`levelShown`, 4 specs). Your white Coal top-up seemed to stop in mid-air on level 7: its last run and its merger are on level 6.
- **Joins that fell back are tried again:** the Rubber top-up into Crystal Oscillators merged up in the factory because its line, on the floor, was boxed in by two belts beside it on the same level, laid before it (a top-up can only meet its line on the line's level, from a side square, and change level only where it turns). Now a join that falls back counts for the retry like a belt left over: next time it's laid right after the belt it joins, at the front; the try with the fewest left over, then the fewest fallbacks, is kept. Router test: a top-up boxed out by two shorter belts beside its line joins it on another try, and the two cross over it.

## Levels picked one by one in 3D (2026-11-18)

The level slider (up to level n, the ones below faded) left the splitters, mergers and the existing belts outside showing, and you wanted to pick exactly the lines of a level. Now a panel on the right of the 3D pop-up has a **checkbox per level** (with *All* and *None*) and the *Machines* switch. Only the lines on the levels ticked show, at full colour; **the splitters and mergers** go with their belt's level where they stand (`lineJoints` gives it), **the existing belts outside** with the levels of the belts joining them (`joinedWays`), and **the factories** (slab and machines) only when a line shown comes from or goes to them (`shownFactories`). My choice: with every level ticked, every factory shows, also one no belt reaches. 3 web specs.

## Your answers to the open questions (2026-11-18)

- **The belt floor doesn't reach the drawn edge**: unchanged, it's whole plots.
- **Pipes may cross over belts**, as belts cross: only straight over straight, at right angles, on another level, so the level stays the same. Still no stacking of pipes with belts. The router now keeps whether each belt is a pipe, not each square, and checks it with the crossings (`Board.Crossings`): a pipe and a belt on one square must cross. Two new tests (a pipe crosses a belt; never on one level). The old test that a pipe never joins a stack of belts still holds, and fails when the new check is taken out.
- **Levels**: 0 on the floor, 1 on one stacked belt, 2 on two. That's how they're numbered already.

## .NET / C# points

| Point | Java / TypeScript |
|---|---|
| **`with` copies a record with one property changed**: `end with { PerMinute = end.PerMinute - taken }`. A positional record's properties are `init`-only, so this is the way to "change" one. | Java records have no withers; you call the constructor with every component again. TypeScript's `{ ...end, perMinute: … }` is the same idea. |
| **A record instead of a long tuple**: `WorldArrangement.Of` returned `(Problem, Grid, Spots, Loads)`, and a fifth value was coming. `MadeArrangement` names them, and `ToDocument` moved onto it. Tuples are fine for two or three values inside a module. Past that, callers deconstruct by position (`(problem, _, _, _, _)`) and break when one is added. | Java has no tuples, so you'd have written the record from the start. |
| **A trailing optional parameter keeps old JSON readable**: `ArrangementProblemDocument(..., Supplies = null)` and `RoutedBeltDocument(..., Load = null)`. System.Text.Json binds the constructor by parameter name, and a parameter missing from stored JSON takes its default. Runs stored before still deserialize. | Jackson with `@JsonCreator` needs `@JsonProperty` defaults handled by hand, or a no-arg constructor. |
| **Local functions inside local functions**: `SuppliesOf` has its own `Add` and a `static` `Ends`. Each uses only what it needs from the method around it. | Java lambdas can't declare named helpers inside them; you'd make private methods and pass the state. |
