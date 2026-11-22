# Step 66 · Buildings past the map's edge

**Goal:** the game lets you build past the edge of its map picture, and your snapshot does. The world map is held to the picture's edges (step 53), so zoomed in, those buildings couldn't be reached. Now they can (asked 2026-11-23). On `feat/no-numbers-in-explanations`, uncommitted, in files of its own.

## What changed

- **`mapExtent(art, world)`** (`world/map/map-model.ts`): the corners the map is held to. That's the picture's, but on each side where a building, belt or pipe stands past it, the side reaches to the furthest one plus `MapMargin` (50 m). Without map art it's null, and nothing holds the map, as before.
- **`MapPage.holdToMapArt`** uses it, and runs again when the snapshot's world arrives (a new `effect`). The zoom out still stops where the whole extent fits, so the buildings past the edge fit too.

Written into ADR-0020, under its map art amendment.

### Technical choices to overrule

- **The margin is 50 m** past the furthest building, so it isn't drawn against the edge.
- **Only the sides with buildings past them grow.** The others stay at the picture's edge.
- **Belts and pipes count by every point of their curves.**

## Tests

| File | What |
|---|---|
| `map-model.spec.ts` (4 new) | Reaches past a building with the margin; past a belt's points; stays the picture when every building is on it; nothing without map art |

249 web tests pass; the build is 464 kB. Not tried in a browser, and Leaflet's bounds aren't unit-tested (jsdom has no canvas).

## TypeScript notes

- **`Math.min(...points)` has a limit:** spreading an array passes each element as an argument, and engines cap the number of arguments (about 100k in V8). A world's belt points could pass that, hence `reduce`. Java's `Collections.min` takes a collection and has no such cap.
