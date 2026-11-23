# Step 84 · The map picture under the optimal arrangement

**Goal:** the optimal arrangement's map showed the world on a plain background; now it has the game's map picture under it, as the world map has (asked 2026-11-23). Branch `feat/ctrl-drag-arrangement`, uncommitted, after step 83.

## How it's built

- **`ArrangementPage`** loads the map art as `MapPage` does (`GET /catalog/map-art`; a 404 before it's imported means none) and lays each tile as a Leaflet image overlay on a layer of its own, added first so everything else lies over it. Not clickable, so it never takes a click meant for a factory or a drag that draws an area.
- **No new geometry:** the tiles come from `mapArtTiles` (`map-model.ts`), as on the world map, and both maps project with `toMapPoint`.
- The page draws the art from an effect, and once more when its map is made: an effect can run before the map exists.

Written into ADR-0020.

### Technical choices to overrule

- **The arrangement's map isn't held to the picture's edges** as the world map is: it can still be moved past them.
- **The loading is repeated** in the two pages (a few lines each) rather than a shared service: the two maps hold different things and load them their own way.

## Tests

1 new, the arrangement page's first spec (Leaflet in jsdom, as the world map's): it lays each tile of the map picture under its map. 316 web tests pass, exit code 0; lint and the format check pass; the build is clean. Not tried in a browser.
