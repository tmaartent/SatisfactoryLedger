# ADR-0024: An area of the map in 3D, drawn with three.js

- **Status:** Accepted
- **Date:** 2026-11-09

## Context
Stacked blueprints are hard to read on the map: it draws each building at its x and y only, so floors cover each other, and a starved top floor can make a whole factory look starved. A prototype ([`docs/prototypes/factory-3d`](../prototypes/factory-3d/README.md)) drew one stacked area of the owner's world in 3D, and the owner found it "incredibly useful"; its front and side views much less so.

Everything needed is in a snapshot already: each building's height, belt and pipe curves in 3D. Only the buildings' shapes were missing: the Catalog kept one flat rectangle per building class (ADR-0020), while the game's Docs file gives each building's clearance boxes in 3D.

## Decision
- **Draw a rectangle on the map, see what's inside it in 3D** (decided 2026-11-09): every building whose position lies inside, at any height, and every belt and pipe with a point inside. Read-only for now; selecting a factory in it comes next.
- **The page is its own route,** `/snapshots/{id}/area?x0=…&y0=…&x1=…&y1=…` (centimetres), so an area can be linked and reloaded.
- **three.js** (npm `three`, with `OrbitControls` and `BufferGeometryUtils` from its addons), in a plain Angular component: the scene is built in `afterNextRender` and redrawn from an `effect`. The logic that needs no WebGL (what lies in the area, where each box's corners go, the swap to three.js's axes) is in `area-model.ts` and tested there.
- **Shapes:** the Catalog keeps each building's clearance boxes in 3D next to its rectangle: a *volume* per box, as the Docs file has it (corners in its own frame, a rotation quaternion, an offset). The snapshot copies them (version 10); the map API returns them. The web app turns each volume into 8 corners, then turns those by the building's yaw and places them.
  - **Without volumes** (a snapshot older than version 10, or a Catalog not re-imported yet), a building is its flat footprint raised to 4 m.
  - **Without a footprint** (splitters, mergers; lifts in a snapshot older than version 11), it's a 2 m cube.
  - **Lifts** (since 2026-11-09, snapshot version 11) have a straight curve from where they stand to their top (the save's `mTopTransform`), and are drawn as belts at their full height. On the 2D map that's a dot where the belts meet.
- **Axes:** the game is left-handed with z up, three.js right-handed with y up. Swapping y and z does both and keeps north at the top when looking down. Buildings are turned in game coordinates first, then converted.
- **Belts and pipes are merged** into one mesh per kind, so a large area stays fast; buildings stay one mesh each, so a click can find them.

## Alternatives considered
- **Front and side elevations in 2D** (Leaflet on another plane): cheaper, but the prototype showed they help much less than turning the scene.
- **Babylon.js or a game engine:** heavier than needed for boxes and tubes; three.js is the common choice and what the prototype used.
- **An endpoint per area:** the map response already holds every building and curve, so the page filters it in the browser. Revisit if worlds grow past what one map response carries comfortably.

## Consequences
- The Catalog must import the Docs file again to fill in the volumes (the migration gives existing footprints none), and a save imported before that keeps flat shapes.
- three.js adds about half a megabyte to the 3D page's lazily loaded bundle, not to the rest of the app.
- The page loads the whole map response, as the map does, and filters it.

## Amendment 2026-11-23: a factory's view is a box, and what isn't connected is faded
A factory's 3D view showed its rectangle at every height, so the factory on the floor above Concrete Below was drawn with it. Decided by the owner:
- **A factory's area is a box:** 15 m past its machines' boxes on every side, below and above too. Only what stands inside is drawn, and belts and pipes are cut off at its sides, floor and roof (three.js clipping planes), so a long belt out stops there. An area drawn on the map keeps every height, and its belts run on, as before.
- **Inside the box, what isn't connected to the factory is faded** (see-through, still clickable, so a neighbouring machine can still be added while editing). Connected (`FactoryDefinition.ConnectedIn`, returned by the factory preview as `connected`): its machines and everything reached from them through logistics, owned or shared, up to the sinks, miners and containers at the lines' ends; the walk stops at another machine. A miner is reached only when nothing but logistics lies between: ore that comes through another factory's smelters stops at those (Reinforced Iron Plates reaches none). And only what's inside the box is drawn. Not only what it owns: a line it shares with another factory, into a sink, is connected too (found on Concrete Below, 2026-11-23).

## Amendment 2026-11-23: bottlenecks and loose ends in red
Finding a bottleneck went: the map, a rectangle around it, the 3D view, and then looking for the belt. Decided by the owner:
- **A bottleneck is red in 3D,** as on the map (BR-A12): belt, lift or pipe.
- **So is a belt, lift or pipe with a loose end**, an end connected to nothing, in the same red: both say *look at this line*, and its Info panel says which (*Bottleneck*, *One end connected to nothing*).
  - The save stores what each connector is linked to, or that it's linked to nothing; the import used to keep only the links. It now counts each conveyor's loose ends (`SavedLogistics.LooseEnds`), the snapshot keeps them (version 14), and the map response passes them on (`MapConveyor.LooseEnds`). Working them out afterwards from the links kept would have failed on pipes, which the import folds into a network hub (BR-A6).
  - A snapshot imported before shows none: the save has to be imported again.
- Tubes are merged per colour, not per kind, so a red belt is its own mesh (`tubeColour`). The legend says what red means.

## Amendment 2026-11-23: shift and drag, no button
Defining a factory in 3D with shift held worked well, so the map does the same (decided by the owner, step 81): a drag with **shift held** draws the rectangle, and letting go opens it in 3D. The *Open an area in 3D* button is gone; the map's header says *Hold shift and drag to see an area in 3D*. The map listens in the capture phase, so it switches Leaflet's panning off before Leaflet starts it; Leaflet's own shift-drag, zooming to a box (`boxZoom`), is off.

## Amendment 2026-11-23: ctrl and drag for the arrangement
The same drag with **ctrl** held (⌘ on a Mac) opens the optimal arrangement of the area instead of the 3D view (decided by the owner, step 83). The header says both.
