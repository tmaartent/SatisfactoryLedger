# Possible enhancements

Ideas the owner wants kept, but not planned yet. Each says what it is, why it's worth doing, and what it would take. When one is picked up, it gets business rules, an ADR where needed, and a step log.

Ideas for practising AI engineering on this project (an MCP server, evals, RAG, fine-tuning with Transformers and PEFT, MLflow) are kept apart, in [AI enhancements](ai-enhancements.md).

## Flow analysis

### Whether vehicles load at a station, and how fast
*Raised 2026-11-10, step 30.*

**What:** read the train timetables, truck routes and drone pairings from the save, to know whether a vehicle loads at a station and roughly how much it takes away per minute.

**Why:** BR-A7 now judges a full station by what the save shows arriving: a running machine feeding it with room in its output. That says *that* something empties it, not *how fast*. Once casings arrive, the station takes all of them, so the casing assemblers use every ingot, and the sheet assemblers sharing those smelters are found short of ingots. In the game the casing line partly backs up and the sheets get their ingots.

**What it would take:** the save's railroad subsystem (timetables, stops, rules per stop), the truck and drone route data, the cargo per trip and the round trip time. A lot of tables for a rate that's still an estimate.

### A full container beside a sink
*Raised 2026-11-11, step 38; kept as is for now.*

**What:** a full container at the end of a line takes nothing while another end of that line (a sink, a container with room, a machine) can take what's made. Only when no other end can does "the save overrules full" (BR-A7) let it take the arriving rate.

**Why:** in the Ballistic Warp Drives factory a smart splitter sends *any* to a container holding 2,400 (full) and *overflow* to an AWESOME Sink. In the game everything goes to the sink. The model shares the arriving 0.965/min evenly over the two ends, so the factory page shows 0.4825 into storage and 0.4825 sunk. The manufacturers run at 100% either way, so their findings are right; only where the output goes is off.

**What it would take:** in the solver, the full-but-arriving inventory takes only what the line's other ends leave, like a depot uploader (BR-A8). A new analysis version, so every snapshot is analysed again.

### An exact steady state, as a linear programme
*Raised 2026-11-05, step 13.*

**What:** compute the steady state as one system of equations and inequalities, solved with a linear-programming solver, instead of sweeping until nothing changes (`SteadyState`). Flows per connection and item are the variables. Capacities, recipes (input and output in fixed ratios at a utilisation between 0 and 1), conservation at every node, and what providers offer are the constraints.

**Why:**
- It's exact, deterministic, and always ends. A degenerate part, where ore supply and downstream room bind at almost the same point, no longer drifts for thousands of sweeps (BR-A10: such parts are now reported as approximate).
- The same technique answers planning questions later: what's the most a factory can make, which belt to upgrade first, how to clock a line.
- The owner works with linear programming in energy demand and consumption forecasting.

**What it would take:**
- **The hard part:** the game's *even* splitting of splitters and mergers (BR-A4, A5) isn't a plain flow constraint. An optimum isn't unique, and an LP picks any corner. It needs extra constraints or a second objective, for example lexicographic max-min fairness, solved in rounds.
- **A solver library for .NET:** for example Google OR-Tools (`Google.OrTools`, with GLOP for LP), or HiGHS through its .NET bindings. That's a new dependency, so it needs an ADR.
- **Size:** a world like the owner's big one has about 45,000 connections and a few items each, so tens of thousands of variables. That's fine for an LP solver.
- **Testing:** the current sweep solver and its tests stay as the reference. Both can run on the same networks and are compared within the tolerance.

### Loops the iterative solver can't push to the top (steps 23 and 25)

The owner's nitric acid and non-fissile uranium blenders each need what the other makes; so do the Ficsonium encoders and colliders. Step 25 starts loops as if running (BR-A10), so they no longer stay at zero. But in a balanced loop with nothing outside it to limit it, **every speed is a steady state**: in the big world the four Ficsonium encoder–collider pairs settle at 4% to 55%, while in the game they run at 100%. Sweeps have no reason to prefer the top. A linear programme maximising flow finds the largest steady state directly. That's now the main reason for it.

### What a truck station burns
*Raised 2026-11-05, step 14. Drones done in step 24 (BR-A16, ADR-0022).*

**What:** compute the fuel a truck station's trucks burn per minute, as step 24 does for drones, instead of taking it from what's left (BR-A8).

**What it would take:**
- **The rate:** the Docs file gives a truck's fuel use only in MW on autopilot (`mAutopilotFuelConsumption`), so it depends on how long its recorded route takes, and how much of it is driven.
- **The route:** recorded per truck in the save. Not verified: the owner's big world has no truck stations; Development.sav has two.

### Drones that wait at a port
*Raised 2026-11-07, step 24.*

A drone's latest trip (BR-A16) is one sample: a drone that waited docked for hours shows a tiny rate, one that just made a fast trip after waiting a long time shows the full one. The game shows the same number, so it's honest, but a few trips averaged would be steadier. The game keeps one (`mTripInformationSampleCount = 1`), so the save can't give more.

### What a generator really burns

Generators are assumed to run at full power (ADR-0021). In the game they throttle down to what their power grid demands. The save has the power circuits; their demand would give each generator its real load, and so its real fuel burn and waste.

## World map

### Buildings drawn as their outline, box by box
*Raised 2026-11-08.*

**What:** draw each building on the map as the outlines of its solid clearance boxes, instead of one rectangle around all of them (ADR-0020).

**Why:** most buildings have one box, so nothing changes for them. But 34 have more, and for those the rectangle covers ground the building doesn't. The hadron collider has 24 boxes: its rectangle is 37 × 27 m, while its real outline is a long arch on a narrower base ([drawn here](https://claude.ai/artifact/AajzFr26K2VvseHqGTfZ5u), private to the owner). A building standing in the empty corner then looks as if it overlaps the collider.

**What it would take:**
- **Catalog:** keep each solid box's outline seen from above, not just the rectangle around them. Most boxes are turned (`RelativeTransform`), so an outline is the hull of a box's 8 corners projected onto the ground, as the drawing does. The parser (`ClearanceDataParser`) already reads the rotations and translations.
- **Contract:** the footprint becomes a list of outlines, each a list of points in the building's own coordinates (centimetres).
- **Map:** a building is drawn as several polygons, each turned the way the building faces (`footprintCorners` per outline). The tooltip stays on the building as a whole.
- **Snapshot:** a new snapshot version for the outlines; older snapshots keep their one rectangle.

### Floors on the map: a height filter, and drawing by height
*Raised 2026-11-08.*

**What:** a height range on the world map, and machines drawn in order of height, so that from above you see one floor at a time.

**Why:** the map draws every building at its x and y only. On stacked blueprints the floors cover each other, and whichever is drawn last hides the rest: a starved top floor can make a whole factory look starved.

**What it would take:** every building in a snapshot already has its height (`PositionDocument.Z`), and belt curves have theirs. A slider on the map page that hides what's outside the range, and sorting the drawing by height. No backend change.

## Factory definitions

### A factory in 3D
*Raised 2026-11-08.*

**Step 26 built the first half:** draw a rectangle on the map and see that area in 3D, with each machine's recipe, stock and findings (ADR-0024). **Step 27** made a factory its machines, with several outputs and inputs (ADR-0025). **Step 37** selects a factory's machines in that view. Still to do (part 2b): a live preview of the traced factory before saving, and the factory page with its outputs and inputs.

**What:** on a factory's page, its members in 3D: machines as their real clearance boxes, belts and pipes along their curves, each machine coloured by the analysis, with a height cut-off and a tooltip per machine. Turn, zoom and move with the mouse.

**Why:** stacked blueprints are hard to read from above. In 3D you see which floor is starved, and the owner found it "incredibly useful" in the [prototype](https://claude.ai/artifact/X76chLgH6ZznvLWbhXNYbC) (private to the owner): 210 machines on floors from 44 to 185 m, drawn smoothly in a browser. Front and side views were less useful there, so they aren't part of it; they'd come for free from the same scene if wanted.

**What it would take:**
- **A factory definition first:** the viewer shows the factory's traced members, not the whole world.
- **3D shapes in the Catalog:** today it keeps one flat rectangle per building (ADR-0020). The importer already reads the full clearance boxes, rotations included; keep them per building class, as the solid boxes' corners. Pairs with [buildings drawn as their outline](#buildings-drawn-as-their-outline-box-by-box).
- **Heights in the API:** a factory's buildings with their full position and yaw, and belt curves with their heights. The snapshot already holds all of it.
- **Splitters and mergers** have no clearance boxes in the Docs file: small cubes, as in the prototype, until their sizes are known. *Lifts are drawn at full height since snapshot version 11 (step 28).*
- **three.js in the web app** (npm `three`, with its `OrbitControls`), so an ADR. One coordinate rule to keep: the game is left-handed with z up, three.js right-handed with y up. Swapping y and z fixes both and keeps north at the top when looking down. Each building is turned by its yaw in game coordinates first, then converted.
- The prototype's code is in [`docs/prototypes/factory-3d/`](../prototypes/factory-3d/README.md): boxes from 8 corners each, Hermite curves sampled into tubes, a perspective camera with orbit controls.

