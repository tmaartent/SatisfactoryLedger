# Step 98 · Pages that survive a failed call

**Goal:** the owner found the snapshot picker greyed out for good (2026-11-25) and asked why Playwright hadn't caught it. Branch `fix/web-api-errors`, from main.

## The cause

Since Angular 20, a `resource`'s `value()` **throws** while the resource is in its error state. The picker read `snapshots.value()` unguarded. When the list failed to load once, for example while the API was still starting, drawing the page threw. The button stayed as it was while loading, and the "couldn't be loaded" hint never showed. Every page read its resources the same way: 28 resources, about 85 reads.

## Why Playwright missed it

- The recorded answers never fail.
- `whole()` checks that the picker is there, not that it's enabled.
- The tests didn't look at errors the page logged.

## What was built

- **`shared/loaded.ts`, `loaded(resource)`:** what a resource loaded, or `undefined` while it loads or after it failed. Every page reads its resources with it (unit test: `loaded.spec.ts`).
- **The picker says so in a snackbar,** with *Try again*. With the sidebar collapsed to its rail, as on a tablet, the picker is too narrow for words: the old hint was hidden there. The import already reports its errors in a snackbar.
- **The arrangement page says when its map couldn't be loaded,** as the other pages do.
- **`world/map/remove-map.ts`:** leaving a map while it zooms threw `_leaflet_pos` (seen in step 96). Leaflet 1.9 ends a zoom from a timer that `remove()` doesn't clear. Its only guard is the private `_animatingZoom` flag, cleared before the map is removed. There is no public call for it.

## Tests

- **Every Playwright test fails when the page threw** (`thrown` fixture in `e2e/test.ts`): an uncaught error, or one Angular logged as `ERROR`.
- **A sweep rule:** every page, with the API answering 503, says "couldn't be loaded" and throws nothing. It failed on all six pages before the fix.
- **The picker:** its list opens; after a failed list, the snackbar's *Try again* loads it (in the browser on every device, and in a unit test).
- **`factory.spec.ts`:** a zoom click gives up after 3 s. When the map had already zoomed to the smelter, the third click waited on a disabled button and timed out the test (3 of 128 runs on main's version, under load).
- **Results:**
  - 398 unit tests pass; so do lint, the format check, the build and `tsc`.
  - Playwright: 269 runs, 219 pass and 50 are skipped as meant, in three of the last five full runs.
  - In the other two, one test failed: *the world map has nothing axe finds* on the iPhone. It didn't fail in 126 runs on its own, and its message wasn't captured. **Not resolved.**
