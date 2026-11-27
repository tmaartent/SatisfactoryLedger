# Step 124 · One 3D stage under both 3D views

**Goal:** card 5 of the architecture review of 2026-11-26: the scene mechanics that `AreaPage` and `BeltFloor3d` each copied move into one module, so a third 3D view comes free and taps are tested once. A refactor, with one agreed behaviour change (Q4). Built with `/implement` in the grilling's session (Q7); branch `refactor/3d-stage`, from `main`.

## Design

- **Module:** `Stage` (`web/src/app/world/stage/stage.ts`), a plain class (Q2). It owns the renderer at its element's size, the camera and its orbit controls, the background, the draw loop, disposing (now including the `ResizeObserver`, which neither view disconnected), a press versus a drag, pointer to hits, moving the camera to frame a box, projecting a point to the screen, and the layer of arrows (Q1).
- **Interface (Q8):** `new Stage(element, { far, canvasFirst?, clipping? }, makeRenderer?)`; `scene`, `canvas`, `orbiting`; `onPress(press => …)` with `press.hits(among)`; `hitsAt(event, among)`; `frame(box, from, distance)`; `onScreen(point)`; `showArrows(arrows)`; `dispose()`.
- **The seam (Q3):** `makeRenderer` gives a `StageRenderer`, the part of `WebGLRenderer` the stage uses. Production passes nothing and gets WebGL; `stage.spec.ts` passes a fake with a real canvas. Two adapters, so a real seam.
- **What stays in the views (Q9):** what they draw, materials, lights; the area's clipping, box-select and conveyor pieces; the floor's hover labels and its three press handlers (pick a factory, toggle a splitter's arrows, a label on touch); each view's own framing direction and distance rule (Q5); where the arrows go (`attachmentArrows`, `jointArrows`).
- **Moved (Q6):** `flow-arrow.ts`, `scene-background.ts` and its spec from `area/` to `stage/`; `ClickReach` from `area/selection.ts` to the stage as `PressReach`, and `ScreenPoint` with it, both of which `selection.ts` now imports from there; one `FlowArrow` type for an arrow, in `flow-arrow.ts`.
- **Deletion test:** delete `Stage` and its 180 lines go back into both views as two copies, with the ray maths three times in the belt floor. It earns its keep: 12 tests, and a third 3D view needs only what it draws.

## How it was built (`/tdd`, seams agreed in Q8 to Q10)

Each slice started red (a compile error: no such member), then went green.

1. The canvas goes into the element, first or last; it's drawn at the element's size and again after a resize; the loop draws until `dispose`, which lets go of the renderer and the observer; clipping only when asked: 4 tests.
2. `onPress`: what lies under it, nearest first; nothing beside it; still a press at 4 px, not at 5: 3 tests.
3. **Dropped:** `lineReach`. three.js's `Raycaster` already finds a line within 1 unit (`params.Line.threshold`), so the belt floor's explicit 1 metre was the default. Building the option would have been speculative generality; the belt floor's override went, with no change.
4. `frame` and `onScreen`: looks at the middle from the side given, as far back as the view says; nothing behind the camera; a press in the middle of the screen finds what's framed: 3 tests.
5. `showArrows`: replaces the earlier arrows; none for an empty list: 2 tests.
6. BeltFloor3d onto the stage, then AreaPage; `orbiting` added with its one caller, the area's boxing.

**Tests changed:** none of the existing ones; `selection.spec.ts` (`isTap`) passes with `PressReach` imported from the stage.

**Test runs:** web 487 passed (41 files); lint and `ng build` clean; e2e on recorded answers 261 passed, 48 skipped (device-specific), the same as before the step; the journey against the real stack passed (it defines a factory with the area's taps and boxes).

**Not covered:** a click on the belt floor. Its e2e tests check that it draws and its Levels tab, not a pick. I probed the recorded arrangement with a grid of 63 clicks, with the Factories tab open: none picked a factory, **on `main`'s code as well as this branch's**, so the probe can't tell; it says nothing about a regression either way. The stage's presses and hits are unit-tested; BeltFloor3d's handlers on top of them aren't. The owner asked for an issue: #67, an e2e test that presses a factory, a splitter and a label on the belt floor; how it finds a factory's slab on screen is still to decide there.

## Angular / TypeScript / three.js points

| Here | In Java / Spring |
|---|---|
| **The stage is a plain class a component holds**, made in `afterNextRender` (the DOM element exists then) and disposed in `DestroyRef.onDestroy`. Primer: *The 3D stage*. | A plain object a Spring component creates in `@PostConstruct` and closes in `@PreDestroy`. |
| **A factory for the one part that needs a GPU:** `makeRenderer: () => StageRenderer = webGl`. Primer: *three.js mostly runs without WebGL*. | A `Clock` or `DataSource` passed in, so a test can stand in for it. |
| **`Pick<THREE.WebGLRenderer, …> & { setAnimationLoop(…) }`**: the type of the seam is cut out of the real class, so the real one fits without an adapter. `setAnimationLoop` is written out because the real signature takes a frame callback with arguments the stage doesn't use. | An interface extracted from a class (`implements`); TypeScript's structural types make it fit without the class naming it. |
| **A getter and setter pair** (`get orbiting()` / `set orbiting(on)`) reads like a field to the caller: `stage.orbiting = false`. | A JavaBean property, but called with field syntax. |
| **An `effect` only hears the signals its last run read**, so `drawAttachmentArrows` reads them all before it returns when there's no stage yet. Primer: *An `effect` only hears the signals its last run read*. | No counterpart; closest is a cache key that forgets an input. |

## Review

Two axes, `/code-review` against `main`. Fixed:

- **`Tap` clashed with the glossary** (Standards, hard): a **Tap** is a consumer on a line. The stage's type is now `Press`, `onTap` is `onPress`, `ClickReach` is `PressReach` (the owner's choice); **Press** is a glossary term, beside **3D Stage**.
- **A stale doc comment** on `BeltFloor3d.toggleArrows` (two in a row): the new one went, the dated one stays.
- **Data clumps:** the arrow's `{ at, along, way }` written out in three places is one `FlowArrow`; `Press` and `onScreen` use `ScreenPoint`, which moved from `area/selection.ts` to the stage, so the stage doesn't depend on the area.
- **`this.stage!`:** the area's `frame`, `machinesOnScreen` and `listenForBoxes` take the stage they use as a parameter; `useBox` checks it once. In the belt floor, `start` hands its stage to the press handler, and `frame` asks `this.stage?.`.

**Overruled at the retro:** Q6(b). The glossary holds the screen's words too, so **3D Stage** and **Press** are glossary terms, under *The 3D views*, and the primer points to them.

Accepted by the owner: `orbiting` (wider than Q8, one caller), `lineReach` not built, and a release with no press before it no longer counting as a press in the area (the area's `down` started at `{ 0, 0 }`; the belt floor's never did). Left: the belt floor's `point()` works out the canvas offset for hover itself, since hover has a pointer event, not a press. Split on the owner's request: the behaviour changes (≤ 4 px, the `ResizeObserver`) went into a `fix:` commit of their own, before the refactor.

## Choices to overrule

- **The press threshold is ≤ 4 px in both views (Q4).** The belt floor counted only < 4 px; a press that moves exactly 4 px now picks there instead of turning the view.
- **`lineReach` isn't built** (Q8 had it): it would have set the default. See slice 3.
- **The area's draw now starts in `start`**, as before, but its content goes into the stage's scene there, not in the constructor: the stage, and so its scene, exists only once the view is on the page.
- **`machinesOnScreen` lost its comment about BR-W5**: since step 123 the draft leaves out another factory's machines, not this function; the comment said otherwise.
- **The canvas in the area goes first** (`canvasFirst`), in the belt floor last: as before; the area's overlays (the box, the choices) come after it.
- **AreaPage shrank by 67 lines (1036 → 969), BeltFloor3d by 75 (714 → 639);** the stage is 182 with its doc comments.

**Files:** `web/src/app/world/stage/stage.ts`, `stage.spec.ts` (new); `stage/flow-arrow.ts`, `stage/scene-background.ts`, `stage/scene-background.spec.ts` (moved from `area/`); `world/area/area-page.ts`, `area/selection.ts`; `world/arrangement/belt-floor-3d.ts`; `docs/guide/primer.md`; this log.
