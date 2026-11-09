# Possible enhancements

Ideas the owner wants kept, but not planned yet. Each says what it is, why it's worth doing, and what it would take. When one is picked up, it gets business rules, an ADR where needed, and a step log.

Ideas for practising AI engineering on this project (an MCP server, evals, RAG, fine-tuning with Transformers and PEFT, MLflow) are kept apart, in [AI enhancements](ai-enhancements.md).

## Flow analysis

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

### A factory drawn as a rectangle on the map
*Raised 2026-11-08.*

**What:** draw a rectangle around a factory on the map, and get its definition proposed from what's inside: the output belt and the input items (BR-W*). You confirm it, and it's stored as any definition is.

**Why:** on the map a factory's layout is plain to see. Picking its output belt from a list is not.

**How it would work:**
- **What's inside:** the buildings, and the belts and pipes whose curves lie within the rectangle.
- **Inputs from the edge:** belts and pipes entering the rectangle; their items are the input items. A belt that passes straight through without touching a machine inside is ignored.
- **Output from the edge:** belts and pipes leaving it, or a storage container inside that nothing leaves (BR-A7). When there are several, rank them by how advanced their item is, and let the player choose:
  - **sink points** (`mResourceSinkPoints`, already read by the importer's `ItemDto`, not stored by the Catalog yet), roughly growing with an item's complexity;
  - or **recipe depth** (production steps from raw resources), more faithful but ambiguous with alternate recipes.
  - Fallback when no belt leaves: the machine inside with the most advanced product.
- **A check for free:** compare the trace from the chosen output belt with the rectangle's contents. A machine inside the trace doesn't reach is probably a separate line; a machine outside the trace reaches is probably a supplier.

**Open questions (the owner's):**
1. **Tool or definition?** The rectangle only proposes today's definition, or a factory *is* an area on the map (membership by place, not by flow; it would survive rebuilds inside it). Leaning: a tool first, and decide the rest after using it.
2. **One output or several** per factory. Today's definition has one output belt; many factories have two (plastic and rubber, or a byproduct).
3. **Floors.** A rectangle is flat and factories are stacked: a height range, or one floor at a time?

**What it would take:** drawing on the map (its first editing interaction: a Leaflet drawing plugin, so an ADR); an endpoint that takes a rectangle and returns a proposed definition with its edge crossings and mismatches; sink points in the Catalog if they're used for ranking. It gets better with [buildings drawn as their outline](#buildings-drawn-as-their-outline-box-by-box), since rectangles around large buildings then catch less.

### A factory in 3D
*Raised 2026-11-08.*

**Step 26 built the first half:** draw a rectangle on the map and see that area in 3D, with each machine's recipe, stock and findings (ADR-0024). Still to do: selecting a factory's machines in that view and defining the factory from them.

**What:** on a factory's page, its members in 3D: machines as their real clearance boxes, belts and pipes along their curves, each machine coloured by the analysis, with a height cut-off and a tooltip per machine. Turn, zoom and move with the mouse.

**Why:** stacked blueprints are hard to read from above. In 3D you see which floor is starved, and the owner found it "incredibly useful" in the [prototype](https://claude.ai/artifact/X76chLgH6ZznvLWbhXNYbC) (private to the owner): 210 machines on floors from 44 to 185 m, drawn smoothly in a browser. Front and side views were less useful there, so they aren't part of it; they'd come for free from the same scene if wanted.

**What it would take:**
- **A factory definition first:** the viewer shows the factory's traced members, not the whole world.
- **3D shapes in the Catalog:** today it keeps one flat rectangle per building (ADR-0020). The importer already reads the full clearance boxes, rotations included; keep them per building class, as the solid boxes' corners. Pairs with [buildings drawn as their outline](#buildings-drawn-as-their-outline-box-by-box).
- **Heights in the API:** a factory's buildings with their full position and yaw, and belt curves with their heights. The snapshot already holds all of it.
- **Lifts, splitters and mergers** have no clearance boxes in the Docs file: small cubes, as in the prototype, until their sizes are known. A lift's height would come from the save.
- **three.js in the web app** (npm `three`, with its `OrbitControls`), so an ADR. One coordinate rule to keep: the game is left-handed with z up, three.js right-handed with y up. Swapping y and z fixes both and keeps north at the top when looking down. Each building is turned by its yaw in game coordinates first, then converted.
- The prototype's code is in [`docs/prototypes/factory-3d/`](../prototypes/factory-3d/README.md): boxes from 8 corners each, Hermite curves sampled into tubes, a perspective camera with orbit controls.

