# Step 88 · Existing belts outside the area, in 3D as built

**Goal:** in a run's 3D belt floor, the existing belts outside the area were drawn flat on the ground, though the save knows their x, y and z. Draw them as they are in 3D (asked 2026-11-23). Branch `feat/ctrl-drag-arrangement`, uncommitted, after step 87.

## How it's built

- **The height was dropped where a way is made** (`ExistingWays`): its points came from the save's curves and positions, seen from above. Now they keep z: `WorldPoint` has an optional `Z`, null where only the view from above is known (the plots, the floor). The curve is sampled in z as in x and y (the same Hermite spline), a building's place gives its z, and where the way crosses into the area the height is interpolated between the two points either side.
- **Stored with the run:** `WayPointDocument` has an optional `Z` too, so a run's problem keeps it, and the run's answer gives it (`ExistingWayResponse`, `api/openapi.json`). A run made before has none.
- **In 3D** (`belt-floor-3d.ts`): the scene has no absolute height: its ground is the belt floor under the arranged factories, which isn't in the save. So a way is anchored where it comes into the area, on the floor where the routed belt joins it, and every point is drawn at its height relative to that (`wayHeight`, `arrangement-model.ts`): above the floor where the belt runs higher in the game, below where it runs lower. The links from there to the routed belt lie on the floor. The miner's drum sits at the height the way starts at.

Written into ADR-0027.

### To see it

**Run the arrangement again**: a run made before this keeps no heights, and its ways stay flat.

### Technical choices to overrule

- **Anchored at the entry**, not at an absolute height: the floor isn't a height in the save, so a way can't be put at its true one; the shape from there on is true.
- **Below the floor** where the belt runs lower than where it comes in, rather than cut off.

## Tests

- .NET: a way keeps its belts' heights along their curves (halfway up a bend climbing 8 m, 4 m); it comes into the area at the height it has there. Three tests that compared a way's points seen from above now compare their heights too. All .NET tests pass, exit code 0.
- Web: `wayHeight`, at its height relative to the entry, and on the floor where a height isn't known. 338 web tests pass, exit code 0; lint and the format check pass; the build is clean.

Not tried in a browser.
