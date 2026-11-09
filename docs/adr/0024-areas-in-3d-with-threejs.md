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
