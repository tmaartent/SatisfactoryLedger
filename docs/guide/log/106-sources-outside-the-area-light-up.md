# Step 106 · A factory picked lights up its sources outside the area

**Goal:** picking Stators in the Desert run lit a bunch of ore lines, which the owner didn't mind, but they stopped at the area's edge (2026-11-25). The owner knew they go on outside the area. Those belts, and pipes in other cases, should light up too, so the sources involved can be seen. Branch `feat/arrangement-touch`, after step 105.

## What was wrong

A belt from a node, station or drone port outside the area leaves the floor where its existing belt comes in. Outside the area that existing belt is drawn dotted, from the building to the area's edge (a *way*, decided 2026-11-18). The way stood out by "the factory it serves", taken from the first belt joining it. With supply lines, that's the first factory on the line. The Stators' ore came along lines starting at miners for Reinforced Iron Plates, Steel Beams, Cable and others. So since step 104 the legs on the floor lit up, back to the edge, but the ways outside stayed dimmed. Only the one miner feeding Stators first was lit.

## What was built

- **`joinedWays`** (`arrangement-model.ts`) returns, per way, the routed belts that join it (`joining`), as the caller's own belt objects. It's generic in the belt's type for that.
- **On the map** (`drawBelts`), a way lights up when a belt joining it does, by the same rule as the belts (`suppliedBelts`, step 104). That covers its dotted line, the strip across to the floor's edge, the ring where it comes in, and the source's mark. Lit with a factory picked, it's drawn as thick as a lit belt (3 px).
- **In 3D,** the way's line and the drum at its miner or extractor carry their joining belts (`userData['joins']`) and fade with them.
- **Pipes** go the same way: a way is a pipe's as much as a belt's.
- **`standsOut` deleted:** since step 104 nothing in `src/` used it but this, and its test went with it (no production code only tests use).

On the owner's run, picking Stators lights **10** ways outside the area, where it lit 1: every miner whose ore reaches Stators, through every factory on its line.

### Choices to overrule

- **The way keeps the colour of the first factory on its line,** while it lights up for any factory further on. The colour says whose line it is; the lighting says it's involved.

## TypeScript points

- **A generic function that hands back the caller's objects:** `joinedWays<Belt extends Pick<RoutedBeltResponse, …>>(…, belts: readonly Belt[]): { joining: Belt[] }[]`. The caller passes full `RoutedBelt` objects and gets the same objects back, typed as such, which `Set.has` finds by reference. Java's `<B extends RoutedBelt> List<B>` is the same idea.

## Tests

- **Unit:** `joinedWays` names the belts that join a way. It failed first, on compile: there was no `joining`.
- **Playwright** (`e2e/arrangement.spec.ts`, *the optimal arrangement, its sources outside the area*): a run answered by the test, with a supply line from a miner outside the area to Smelting and on to Iron Plates. Picking Iron Plates, the dotted way outside stays lit (opacity 0.8, read from the caught Leaflet map). It failed first on all four devices, at 0.25.
- **Results:**
  - 436 web tests pass, exit code 0; lint, the format check, the build and the e2e type check pass;
  - Playwright: 245 passed, 48 skipped, none failed.
